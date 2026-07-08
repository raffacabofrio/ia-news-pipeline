<?php
/**
 * Single post centerpiece template for S3.2.
 */

get_header();
?>
<main class="single-feature py-4 py-lg-5">
	<?php
	if ( have_posts() ) {
		while ( have_posts() ) {
			the_post();

			$excerpt = trim( wp_strip_all_tags( get_the_excerpt() ) );
			$source_url = trim( (string) get_post_meta( get_the_ID(), '_pipeline_source_url', true ) );

			if ( '' === $source_url ) {
				$source_url = trim( (string) get_post_meta( get_the_ID(), 'source_url', true ) );
			}

			$source_url = '' !== $source_url ? esc_url( $source_url ) : '';
			$model_name = trim( (string) get_post_meta( get_the_ID(), '_pipeline_model', true ) );
			?>
			<article <?php post_class( 'single-feature__article container' ); ?>>
				<div class="single-feature__surface mx-auto">
					<header class="single-feature__hero">
						<div class="single-feature__chrome eyebrow text-secondary">
							<span><?php echo esc_html( get_the_date() ); ?></span>
							<span class="single-feature__divider" aria-hidden="true"></span>
							<span class="single-feature__badge">AI-generated</span>
						</div>
						<h1 class="single-feature__title"><?php the_title(); ?></h1>
						<?php if ( '' !== $excerpt ) { ?>
							<p class="single-feature__lead"><?php echo esc_html( $excerpt ); ?></p>
						<?php } ?>
						<div class="single-feature__meta">
							<span class="single-feature__meta-pill">Classic PHP theme</span>
							<?php if ( '' !== $model_name ) { ?>
								<span class="single-feature__meta-pill">Model: <?php echo esc_html( $model_name ); ?></span>
							<?php } ?>
						</div>
					</header>

					<?php if ( has_post_thumbnail() ) { ?>
						<div class="single-feature__media">
							<?php the_post_thumbnail( 'large', array( 'class' => 'single-feature__image img-fluid' ) ); ?>
						</div>
					<?php } ?>

					<div class="single-feature__measure">
						<div class="single-feature__body">
							<?php the_content(); ?>
						</div>

						<?php if ( '' !== $source_url ) { ?>
							<aside class="single-feature__attribution" aria-label="Source attribution">
								<p class="single-feature__attribution-label eyebrow text-secondary mb-2">Source</p>
								<a class="single-feature__source-link" href="<?php echo $source_url; ?>" target="_blank" rel="noreferrer noopener">
									Read original source
								</a>
							</aside>
						<?php } ?>
					</div>
				</div>
			</article>
			<?php
		}
	}
	?>
</main>
<?php
get_footer();
