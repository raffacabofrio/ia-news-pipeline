#!/bin/sh
# wp-init — one-shot WordPress bootstrap (architecture D5, Story S0.1).
# Runs in the wordpress:cli image, sharing the wordpress service's /var/www/html
# volume and DB env. Idempotent: guarded by `wp core is-installed`, safe on every
# `docker compose up` (AC6). Exits 0 on success — expected in compose output.
set -eu

WP_PATH=/var/www/html
WP_PORT="${WP_PORT:-8080}"
WP_URL="${WP_URL:-http://localhost:${WP_PORT}}"
WP_TITLE="${WP_TITLE:-IA News}"
WP_ADMIN_USER="${WP_ADMIN_USER:-admin}"
WP_ADMIN_PASSWORD="${WP_ADMIN_PASSWORD:-admin}"
WP_ADMIN_EMAIL="${WP_ADMIN_EMAIL:-admin@example.com}"

# wp-cli needs to know apache has mod_rewrite so `wp rewrite ... --hard` writes .htaccess
export WP_CLI_CONFIG_PATH=/tmp/wp-cli.yml
printf 'apache_modules:\n  - mod_rewrite\n' > "$WP_CLI_CONFIG_PATH"

wait_for() {
  # $1 = description, $2 = max tries (2s apart), rest = command
  desc="$1"; max="$2"; shift 2
  tries=0
  until "$@" >/dev/null 2>&1; do
    tries=$((tries + 1))
    if [ "$tries" -ge "$max" ]; then
      echo "[wp-init] ERROR: timed out waiting for ${desc}" >&2
      exit 1
    fi
    echo "[wp-init] waiting for ${desc}... (${tries}/${max})"
    sleep 2
  done
}

check_core_files() { [ -f "${WP_PATH}/wp-config.php" ] && wp core version --path="${WP_PATH}"; }

# DB readiness via PHP mysqli (same driver WordPress uses). Deliberately NOT `wp db check`:
# that shells out to the mariadb client, which since 11.4 verifies TLS certs by default and
# rejects MySQL's self-signed certificate (error 2026: self-signed certificate in chain).
check_db() {
  php -r '
    mysqli_report(MYSQLI_REPORT_OFF);
    $c = @mysqli_connect(
      getenv("WORDPRESS_DB_HOST") ?: "mysql",
      getenv("WORDPRESS_DB_USER") ?: "wordpress",
      getenv("WORDPRESS_DB_PASSWORD") ?: "wordpress",
      getenv("WORDPRESS_DB_NAME") ?: "wordpress"
    );
    exit($c ? 0 : 1);
  '
}

# The wordpress (apache) container copies core files + wp-config.php into the shared volume
# on first start; the DB may also still be warming up.
wait_for "WordPress core files (copied by the wordpress container)" 90 check_core_files
wait_for "database connection" 90 check_db

if wp core is-installed --path="${WP_PATH}" >/dev/null 2>&1; then
  echo "[wp-init] WordPress already installed — skipping core install (idempotent re-run)"
else
  echo "[wp-init] installing WordPress core at ${WP_URL}"
  wp core install \
    --path="${WP_PATH}" \
    --url="${WP_URL}" \
    --title="${WP_TITLE}" \
    --admin_user="${WP_ADMIN_USER}" \
    --admin_password="${WP_ADMIN_PASSWORD}" \
    --admin_email="${WP_ADMIN_EMAIL}" \
    --skip-email
fi

echo "[wp-init] activating placeholder plugin + theme"
wp plugin activate ia-pipeline-receiver --path="${WP_PATH}"
wp theme activate ia-news-theme --path="${WP_PATH}"

echo "[wp-init] setting permalink structure /%postname%/"
wp rewrite structure '/%postname%/' --hard --path="${WP_PATH}"

echo "[wp-init] done — WordPress ready at ${WP_URL}"
