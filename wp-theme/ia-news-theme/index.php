<?php
/**
 * Main fallback template for IA News Theme.
 */

get_header();
?>
<main class="site-shell">
	<section class="site-hero text-white py-5">
		<div class="container py-4">
			<div class="row justify-content-between align-items-end g-4">
				<div class="col-lg-7">
					<p class="eyebrow mb-3">Evaluator-ready scaffold</p>
					<h1 class="display-4 mb-3"><?php bloginfo( 'name' ); ?></h1>
					<p class="lead mb-0">
						A minimal editorial shell backed by local compiled assets, ready for the richer story experience in S3.2.
					</p>
				</div>
				<div class="col-lg-4">
					<div class="bg-white text-dark rounded-4 p-4 shadow-sm">
						<p class="eyebrow text-secondary mb-2">Theme signal</p>
						<p class="mb-0">Bootstrap styles and scripts are loaded from the committed theme build, with no CDN or dev server dependency.</p>
					</div>
				</div>
			</div>
		</div>
	</section>

	<section class="container py-5">
		<div class="row g-4">
			<?php
			if ( have_posts() ) {
				while ( have_posts() ) {
					the_post();
					?>
					<div class="col-12">
						<article <?php post_class( 'news-card bg-white p-4 p-lg-5 h-100 position-relative' ); ?>>
							<p class="eyebrow text-secondary mb-2"><?php echo esc_html( get_the_date() ); ?></p>
							<h2 class="h1 mb-3">
								<a class="link-dark stretched-link" href="<?php the_permalink(); ?>"><?php the_title(); ?></a>
							</h2>
							<div class="fs-5 text-body-secondary">
								<?php the_excerpt(); ?>
							</div>
						</article>
					</div>
					<?php
				}
			} else {
				?>
				<div class="col-12">
					<div class="news-card bg-white p-4 p-lg-5">
						<h2 class="h3 mb-2">No posts yet</h2>
						<p class="mb-0 text-body-secondary">The theme scaffold is active and waiting for the ingestion pipeline to publish content.</p>
					</div>
				</div>
				<?php
			}
			?>
		</div>
	</section>
</main>
<?php
get_footer();
