<?php
/**
 * Minimal single post stub for S3.1.
 */

get_header();
?>
<main class="container py-5">
	<?php
	if ( have_posts() ) {
		while ( have_posts() ) {
			the_post();
			?>
			<article <?php post_class( 'news-card bg-white p-4 p-lg-5' ); ?>>
				<p class="eyebrow text-secondary mb-2"><?php echo esc_html( get_the_date() ); ?></p>
				<h1 class="display-5 mb-3"><?php the_title(); ?></h1>
				<div class="fs-5 lh-lg">
					<?php the_content(); ?>
				</div>
			</article>
			<?php
		}
	}
	?>
</main>
<?php
get_footer();
