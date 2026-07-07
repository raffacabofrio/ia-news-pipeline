-- Story S0.1 — one MySQL instance, two schemas (architecture §1).
-- The `wordpress` schema is created by the image via MYSQL_DATABASE.
-- This script adds the empty `pipeline` schema (jobs table DDL arrives in S1.1)
-- and grants the app user access to both.
-- NOTE: the user name below must match MYSQL_USER in docker-compose.yml (default: wordpress).

CREATE DATABASE IF NOT EXISTS `pipeline`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

GRANT ALL PRIVILEGES ON `pipeline`.* TO 'wordpress'@'%';
FLUSH PRIVILEGES;
