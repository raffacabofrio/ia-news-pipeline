<?php
/**
 * Plugin Name:       IA Pipeline Receiver
 * Plugin URI:        https://github.com/raffacabofrio/ia-news-pipeline
 * Description:       Receives published posts from the IA news pipeline via signed webhook. Placeholder receiver replaced in story S2.1.
 * Version:           0.1.0
 * Requires at least: 6.0
 * Requires PHP:      8.1
 * Author:            ia-news-pipeline
 * License:           MIT
 * Text Domain:       ia-pipeline-receiver
 */

if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

add_action( 'rest_api_init', 'ia_pipeline_register_routes' );

function ia_pipeline_register_routes() {
	register_rest_route(
		'ia-pipeline/v1',
		'/posts',
		array(
			'methods'             => WP_REST_Server::CREATABLE,
			'callback'            => 'ia_pipeline_handle_posts_request',
			'permission_callback' => '__return_true',
		)
	);
}

function ia_pipeline_handle_posts_request( WP_REST_Request $request ) {
	$raw_body = $request->get_body();
	$auth     = ia_pipeline_authenticate_request( $request, $raw_body );

	if ( is_wp_error( $auth ) ) {
		return ia_pipeline_error_response( $auth->get_error_code(), $auth->get_error_message(), 401 );
	}

	$payload = ia_pipeline_validate_payload( $raw_body );
	if ( is_wp_error( $payload ) ) {
		return ia_pipeline_error_response( $payload->get_error_code(), $payload->get_error_message(), 422 );
	}

	$existing_post_id = ia_pipeline_find_existing_post_id( $payload['job_id'] );
	if ( null !== $existing_post_id ) {
		return ia_pipeline_success_response( $existing_post_id, true, 200 );
	}

	return ia_pipeline_create_post_with_idempotency_lock( $payload );
}

function ia_pipeline_authenticate_request( WP_REST_Request $request, $raw_body ) {
	$timestamp_header = $request->get_header( 'x-pipeline-timestamp' );
	$signature_header = $request->get_header( 'x-pipeline-signature' );

	if ( '' === $timestamp_header || '' === $signature_header ) {
		return new WP_Error( 'missing_signature', 'Missing signature headers.' );
	}

	if ( ! preg_match( '/^\d+$/', $timestamp_header ) ) {
		return new WP_Error( 'invalid_timestamp', 'Invalid timestamp header.' );
	}

	if ( 1 !== preg_match( '/^sha256=([a-f0-9]{64})$/', $signature_header ) ) {
		return new WP_Error( 'invalid_signature', 'Invalid signature format.' );
	}

	$secret = ia_pipeline_get_shared_secret();
	if ( '' === $secret ) {
		return new WP_Error( 'missing_secret', 'Receiver secret is not configured.' );
	}

	$timestamp = (int) $timestamp_header;
	if ( abs( time() - $timestamp ) > 300 ) {
		return new WP_Error( 'stale_signature', 'Stale timestamp.' );
	}

	$expected_signature = 'sha256=' . hash_hmac( 'sha256', $timestamp_header . '.' . $raw_body, $secret );
	if ( ! hash_equals( $expected_signature, $signature_header ) ) {
		return new WP_Error( 'invalid_signature', 'Signature verification failed.' );
	}

	return true;
}

function ia_pipeline_get_shared_secret() {
	if ( defined( 'PIPELINE_SHARED_SECRET' ) && is_string( PIPELINE_SHARED_SECRET ) ) {
		return trim( PIPELINE_SHARED_SECRET );
	}

	$secret = getenv( 'PIPELINE_SHARED_SECRET' );
	if ( false !== $secret && '' !== trim( $secret ) ) {
		return trim( $secret );
	}

	if ( isset( $_ENV['PIPELINE_SHARED_SECRET'] ) && is_string( $_ENV['PIPELINE_SHARED_SECRET'] ) ) {
		return trim( $_ENV['PIPELINE_SHARED_SECRET'] );
	}

	if ( isset( $_SERVER['PIPELINE_SHARED_SECRET'] ) && is_string( $_SERVER['PIPELINE_SHARED_SECRET'] ) ) {
		return trim( $_SERVER['PIPELINE_SHARED_SECRET'] );
	}

	return '';
}

function ia_pipeline_validate_payload( $raw_body ) {
	$payload = json_decode( $raw_body, true );
	if ( JSON_ERROR_NONE !== json_last_error() || ! is_array( $payload ) ) {
		return new WP_Error( 'invalid_json', 'Malformed JSON payload.' );
	}

	$required_fields = array( 'job_id', 'source_url', 'title', 'content_html', 'excerpt', 'meta' );
	foreach ( $required_fields as $field ) {
		if ( ! array_key_exists( $field, $payload ) ) {
			return new WP_Error( 'invalid_payload', sprintf( 'Missing required field: %s.', $field ) );
		}
	}

	$job_id       = ia_pipeline_require_non_empty_string( $payload['job_id'], 'job_id' );
	$source_url   = ia_pipeline_require_non_empty_string( $payload['source_url'], 'source_url' );
	$title        = ia_pipeline_require_non_empty_string( $payload['title'], 'title' );
	$content_html = ia_pipeline_require_non_empty_string( $payload['content_html'], 'content_html' );
	$excerpt      = ia_pipeline_require_non_empty_string( $payload['excerpt'], 'excerpt' );

	foreach ( array( $job_id, $source_url, $title, $content_html, $excerpt ) as $candidate ) {
		if ( is_wp_error( $candidate ) ) {
			return $candidate;
		}
	}

	if ( false === filter_var( $source_url, FILTER_VALIDATE_URL ) ) {
		return new WP_Error( 'invalid_payload', 'source_url must be a valid URL.' );
	}

	$meta = $payload['meta'];
	if ( ! is_array( $meta ) ) {
		return new WP_Error( 'invalid_payload', 'meta must be an object.' );
	}

	$model        = ia_pipeline_require_non_empty_string( $meta['model'] ?? null, 'meta.model' );
	$generated_at = ia_pipeline_require_non_empty_string( $meta['generated_at'] ?? null, 'meta.generated_at' );

	if ( is_wp_error( $model ) ) {
		return $model;
	}

	if ( is_wp_error( $generated_at ) ) {
		return $generated_at;
	}

	if ( false === strtotime( $generated_at ) ) {
		return new WP_Error( 'invalid_payload', 'meta.generated_at must be a valid datetime.' );
	}

	$sanitized_title = sanitize_text_field( $title );
	if ( '' === $sanitized_title ) {
		return new WP_Error( 'invalid_payload', 'title must contain visible text.' );
	}

	$sanitized_excerpt = sanitize_textarea_field( $excerpt );
	if ( '' === $sanitized_excerpt ) {
		return new WP_Error( 'invalid_payload', 'excerpt must contain visible text.' );
	}

	$sanitized_content = wp_kses_post( $content_html );
	if ( '' === trim( $sanitized_content ) ) {
		return new WP_Error( 'invalid_payload', 'content_html must contain allowed post content.' );
	}

	return array(
		'job_id'       => $job_id,
		'source_url'   => $source_url,
		'title'        => $sanitized_title,
		'content_html' => $sanitized_content,
		'excerpt'      => $sanitized_excerpt,
		'meta'         => array(
			'model'        => $model,
			'generated_at' => $generated_at,
		),
	);
}

function ia_pipeline_require_non_empty_string( $value, $field_name ) {
	if ( ! is_string( $value ) ) {
		return new WP_Error( 'invalid_payload', sprintf( '%s must be a string.', $field_name ) );
	}

	$trimmed = trim( $value );
	if ( '' === $trimmed ) {
		return new WP_Error( 'invalid_payload', sprintf( '%s must not be empty.', $field_name ) );
	}

	return $trimmed;
}

function ia_pipeline_find_existing_post_id( $job_id ) {
	$posts = get_posts(
		array(
			'post_type'      => 'post',
			'post_status'    => 'any',
			'meta_key'       => '_pipeline_job_id',
			'meta_value'     => $job_id,
			'fields'         => 'ids',
			'posts_per_page' => 1,
			'no_found_rows'  => true,
		)
	);

	if ( empty( $posts ) ) {
		return null;
	}

	return (int) $posts[0];
}

function ia_pipeline_create_post_with_idempotency_lock( array $payload ) {
	$lock_key = ia_pipeline_job_lock_key( $payload['job_id'] );
	if ( ! add_option( $lock_key, '1', '', false ) ) {
		$existing_post_id = ia_pipeline_wait_for_existing_post_id( $payload['job_id'] );
		if ( null !== $existing_post_id ) {
			return ia_pipeline_success_response( $existing_post_id, true, 200 );
		}

		return ia_pipeline_error_response( 'duplicate_in_progress', 'A delivery for this job is already being processed.', 409 );
	}

	$post_id = null;

	try {
		$existing_post_id = ia_pipeline_find_existing_post_id( $payload['job_id'] );
		if ( null !== $existing_post_id ) {
			return ia_pipeline_success_response( $existing_post_id, true, 200 );
		}

		$post_id = ia_pipeline_create_post( $payload );
		if ( is_wp_error( $post_id ) ) {
			return ia_pipeline_error_response( 'post_creation_failed', 'Unable to create post.', 500 );
		}

		return ia_pipeline_success_response( $post_id, false, 201 );
	} finally {
		delete_option( $lock_key );
	}
}

function ia_pipeline_job_lock_key( $job_id ) {
	return 'ia_pipeline_job_lock_' . md5( $job_id );
}

function ia_pipeline_wait_for_existing_post_id( $job_id, $attempts = 50, $sleep_microseconds = 100000 ) {
	for ( $attempt = 0; $attempt < $attempts; $attempt++ ) {
		$existing_post_id = ia_pipeline_find_existing_post_id( $job_id );
		if ( null !== $existing_post_id ) {
			return $existing_post_id;
		}

		usleep( $sleep_microseconds );
	}

	return null;
}

function ia_pipeline_create_post( array $payload ) {
	$post_id = wp_insert_post(
		array(
			'post_type'    => 'post',
			'post_status'  => 'publish',
			'post_title'   => $payload['title'],
			'post_content' => $payload['content_html'],
			'post_excerpt' => $payload['excerpt'],
		),
		true
	);

	if ( is_wp_error( $post_id ) ) {
		return $post_id;
	}

	update_post_meta( $post_id, '_pipeline_job_id', $payload['job_id'] );
	update_post_meta( $post_id, '_pipeline_source_url', esc_url_raw( $payload['source_url'] ) );
	update_post_meta( $post_id, '_pipeline_model', $payload['meta']['model'] );
	update_post_meta( $post_id, '_pipeline_generated_at', $payload['meta']['generated_at'] );

	return (int) $post_id;
}

function ia_pipeline_success_response( $post_id, $duplicate, $status_code ) {
	return new WP_REST_Response(
		array(
			'post_id'   => (int) $post_id,
			'post_url'  => get_permalink( $post_id ),
			'duplicate' => (bool) $duplicate,
		),
		$status_code
	);
}

function ia_pipeline_error_response( $code, $reason, $status_code ) {
	return new WP_REST_Response(
		array(
			'code'   => $code,
			'reason' => $reason,
		),
		$status_code
	);
}
