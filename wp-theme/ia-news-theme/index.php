<?php
/**
 * IA News Theme — placeholder index template (Story S0.1).
 * Real templates (single.php centerpiece, Bootstrap/SASS via Vite) arrive in S3.1/S3.2.
 */
?>
<!doctype html>
<html <?php language_attributes(); ?>>
<head>
	<meta charset="<?php bloginfo( 'charset' ); ?>">
	<meta name="viewport" content="width=device-width, initial-scale=1">
	<?php wp_head(); ?>
</head>
<body <?php body_class(); ?>>
	<main>
		<h1><?php bloginfo( 'name' ); ?></h1>
		<p><em>IA News Theme placeholder — real theme arrives in S3.1/S3.2.</em></p>
		<?php
		if ( have_posts() ) {
			while ( have_posts() ) {
				the_post();
				?>
				<article <?php post_class(); ?>>
					<h2><a href="<?php the_permalink(); ?>"><?php the_title(); ?></a></h2>
					<div><?php the_content(); ?></div>
				</article>
				<?php
			}
		} else {
			echo '<p>No posts yet.</p>';
		}
		?>
	</main>
	<?php wp_footer(); ?>
</body>
</html>
