<?php
/**
 * Theme bootstrap for IA News Theme.
 */

if ( ! function_exists( 'ia_news_theme_get_asset_manifest' ) ) {
	function ia_news_theme_get_asset_manifest() {
		static $manifest = null;

		if ( null !== $manifest ) {
			return $manifest;
		}

		$manifest_path = get_theme_file_path( 'assets/dist/.vite/manifest.json' );

		if ( ! file_exists( $manifest_path ) ) {
			$manifest = array();
			return $manifest;
		}

		$decoded  = json_decode( file_get_contents( $manifest_path ), true );
		$manifest = is_array( $decoded ) ? $decoded : array();

		return $manifest;
	}
}

if ( ! function_exists( 'ia_news_theme_asset_uris' ) ) {
	function ia_news_theme_asset_uris( $entry, $field = 'file' ) {
		$manifest = ia_news_theme_get_asset_manifest();

		if ( ! isset( $manifest[ $entry ][ $field ] ) ) {
			return array();
		}

		$asset = $manifest[ $entry ][ $field ];
		$paths = is_array( $asset ) ? $asset : array( $asset );

		return array_map(
			static function ( $path ) {
				return get_theme_file_uri( 'assets/dist/' . ltrim( $path, '/' ) );
			},
			$paths
		);
	}
}

function ia_news_theme_enqueue_assets() {
	$style_uris = ia_news_theme_asset_uris( 'src/scripts/main.js', 'css' );
	$script_uris = ia_news_theme_asset_uris( 'src/scripts/main.js', 'file' );

	foreach ( $style_uris as $index => $style_uri ) {
		$handle = 0 === $index ? 'ia-news-theme' : 'ia-news-theme-' . $index;
		wp_enqueue_style( $handle, $style_uri, array(), null );
	}

	$script_uri = reset( $script_uris );
	if ( $script_uri ) {
		wp_enqueue_script( 'ia-news-theme', $script_uri, array(), null, true );
	}
}
add_action( 'wp_enqueue_scripts', 'ia_news_theme_enqueue_assets' );

function ia_news_theme_setup() {
	add_theme_support( 'title-tag' );
	add_theme_support( 'post-thumbnails' );
}
add_action( 'after_setup_theme', 'ia_news_theme_setup' );
