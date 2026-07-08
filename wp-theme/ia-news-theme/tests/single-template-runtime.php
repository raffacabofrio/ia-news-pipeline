<?php

$theme_root = dirname( __DIR__ );
$fixture = array();
$post_available = false;

function assert_true( $condition, $message ) {
	if ( ! $condition ) {
		fwrite( STDERR, $message . PHP_EOL );
		exit( 1 );
	}
}

function get_header() {
	echo '<header-stub>';
}

function get_footer() {
	echo '<footer-stub>';
}

function have_posts() {
	global $post_available;

	return $post_available;
}

function the_post() {
	global $post_available;

	$post_available = false;
}

function get_the_ID() {
	return 42;
}

function get_the_date() {
	global $fixture;

	return $fixture['date'];
}

function the_title() {
	global $fixture;

	echo htmlspecialchars( $fixture['title'], ENT_QUOTES, 'UTF-8' );
}

function get_the_excerpt() {
	global $fixture;

	return $fixture['excerpt'];
}

function the_content() {
	global $fixture;

	echo $fixture['content'];
}

function get_post_meta( $post_id, $key, $single = false ) {
	global $fixture;

	return $fixture['meta'][ $key ] ?? '';
}

function has_post_thumbnail() {
	global $fixture;

	return ! empty( $fixture['thumbnail'] );
}

function the_post_thumbnail( $size, $attributes = array() ) {
	global $fixture;

	$class = $attributes['class'] ?? '';
	echo '<img class="' . htmlspecialchars( $class, ENT_QUOTES, 'UTF-8' ) . '" src="https://example.test/image.jpg" alt="' . htmlspecialchars( $fixture['title'], ENT_QUOTES, 'UTF-8' ) . '">';
}

function wp_strip_all_tags( $text ) {
	return trim( strip_tags( $text ) );
}

function wp_kses_post( $content ) {
	return $content;
}

function esc_html( $text ) {
	return htmlspecialchars( (string) $text, ENT_QUOTES, 'UTF-8' );
}

function esc_url( $url ) {
	if ( false === filter_var( $url, FILTER_VALIDATE_URL ) ) {
		return '';
	}

	return htmlspecialchars( $url, ENT_QUOTES, 'UTF-8' );
}

function post_class( $class = '' ) {
	echo 'class="' . htmlspecialchars( $class, ENT_QUOTES, 'UTF-8' ) . '"';
}

function render_single_template( $next_fixture ) {
	global $fixture, $post_available, $theme_root;

	$fixture = $next_fixture;
	$post_available = true;

	ob_start();
	require $theme_root . '/single.php';
	return ob_get_clean();
}

$rendered = render_single_template(
	array(
		'title'     => 'Signal from the queue',
		'date'      => 'July 7, 2026',
		'excerpt'   => 'A concise lead paragraph that should stand apart from the body copy.',
		'content'   => '<p>Body paragraph one.</p><p>Body paragraph two.</p>',
		'thumbnail' => true,
		'meta'      => array(
			'_pipeline_job_id'     => 'job-42',
			'_pipeline_source_url' => 'https://example.com/original-story',
			'_pipeline_model'      => 'gpt-5',
		),
	)
);

if ( getenv( 'DUMP_RENDERED_SINGLE' ) ) {
	fwrite( STDERR, $rendered . PHP_EOL );
}

assert_true( str_contains( $rendered, 'single-feature__badge' ), 'Expected visible AI-generated badge markup.' );
assert_true( str_contains( $rendered, 'AI-generated' ), 'Expected AI-generated badge label.' );
assert_true( str_contains( $rendered, 'single-feature__lead' ), 'Expected excerpt lead markup when excerpt exists.' );
assert_true( str_contains( $rendered, 'single-feature__measure' ), 'Expected constrained reading measure wrapper.' );
assert_true( str_contains( $rendered, 'single-feature__attribution' ), 'Expected attribution block when source URL meta exists.' );
assert_true( str_contains( $rendered, 'Read original source' ), 'Expected source attribution link copy.' );
assert_true( str_contains( $rendered, 'https://example.com/original-story' ), 'Expected source link href to come from pipeline meta.' );
assert_true( str_contains( $rendered, 'Model: gpt-5' ), 'Expected model metadata pill when model meta exists.' );

$rendered_without_source = render_single_template(
	array(
		'title'     => 'Fallback article',
		'date'      => 'July 7, 2026',
		'excerpt'   => '',
		'content'   => '<p>Body paragraph only.</p>',
		'thumbnail' => false,
		'meta'      => array(
			'_pipeline_job_id' => 'job-43',
			'_pipeline_model'  => 'gpt-5-mini',
		),
	)
);

assert_true( ! str_contains( $rendered_without_source, 'single-feature__attribution' ), 'Attribution block should disappear cleanly when source URL is missing.' );
assert_true( ! str_contains( $rendered_without_source, 'single-feature__lead' ), 'Lead paragraph should not render when excerpt is empty.' );
