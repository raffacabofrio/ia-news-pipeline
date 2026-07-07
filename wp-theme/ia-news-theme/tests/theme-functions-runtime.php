<?php

$theme_root = dirname( __DIR__ );
$manifest_path = $theme_root . '/assets/dist/.vite/manifest.json';
$backup_path = $manifest_path . '.bak';
$enqueued_styles = array();
$enqueued_scripts = array();

function assert_true( $condition, $message ) {
	if ( ! $condition ) {
		fwrite( STDERR, $message . PHP_EOL );
		exit( 1 );
	}
}

function get_theme_file_path( $path = '' ) {
	global $theme_root;

	return $theme_root . '/' . ltrim( $path, '/' );
}

function get_theme_file_uri( $path = '' ) {
	return 'http://example.test/wp-content/themes/ia-news-theme/' . ltrim( $path, '/' );
}

function add_action( $hook, $callback ) {
}

function wp_enqueue_style( $handle, $src, $deps = array(), $ver = null ) {
	global $enqueued_styles;

	$enqueued_styles[] = compact( 'handle', 'src' );
}

function wp_enqueue_script( $handle, $src, $deps = array(), $ver = null, $in_footer = false ) {
	global $enqueued_scripts;

	$enqueued_scripts[] = compact( 'handle', 'src', 'in_footer' );
}

function add_theme_support( $feature ) {
}

require $theme_root . '/functions.php';

assert_true( is_array( ia_news_theme_get_asset_manifest() ), 'Manifest loader should always return an array.' );

rename( $manifest_path, $backup_path );
try {
	file_put_contents( $manifest_path, '{"broken":' );
	$broken_manifest = ia_news_theme_read_asset_manifest( $manifest_path );
	assert_true( array() === $broken_manifest, 'Corrupted manifest should fail closed with an empty array.' );
} finally {
	unlink( $manifest_path );
	rename( $backup_path, $manifest_path );
}

$script_uris = ia_news_theme_asset_uris( 'src/scripts/main.js', 'file' );
assert_true( 1 === count( $script_uris ), 'Expected one built script URI from the manifest.' );
assert_true(
	str_contains( $script_uris[0], 'assets/dist/assets/theme-' ),
	'Expected manifest-derived script URI to point at committed theme assets.'
);

ia_news_theme_enqueue_assets();
assert_true( count( $enqueued_styles ) >= 1, 'Expected at least one stylesheet to be enqueued.' );
assert_true( 1 === count( $enqueued_scripts ), 'Expected one script to be enqueued.' );
assert_true(
	true === $enqueued_scripts[0]['in_footer'],
	'Expected the theme bundle to load in the footer.'
);
