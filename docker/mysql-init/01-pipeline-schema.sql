-- Story S0.1 — one MySQL instance, two schemas (architecture §1).
-- The `wordpress` schema is created by the image via MYSQL_DATABASE.
-- This script adds the empty `pipeline` schema (jobs table DDL arrives in S1.1)
-- Grants are applied by 02-pipeline-grants.sh so MYSQL_USER remains configurable.

CREATE DATABASE IF NOT EXISTS `pipeline`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE `pipeline`;

CREATE TABLE IF NOT EXISTS `jobs` (
  `job_id` CHAR(36) NOT NULL,
  `state` ENUM('queued', 'processing', 'publishing', 'published', 'failed') NOT NULL,
  `source_url` TEXT NOT NULL,
  `failure_reason` TEXT NULL,
  `published_post_url` TEXT NULL,
  `rewrite_model` VARCHAR(128) NULL,
  `generated_at_utc` DATETIME NULL,
  `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`job_id`),
  INDEX `ix_jobs_state_created_at` (`state`, `created_at`)
);

-- Story S1.2 — worker persists rewrite metadata (`processing`/`publishing`/`published`
-- transitions in MySqlJobStore) alongside the existing failure/publish fields. Additive
-- ALTERs cover the case where `jobs` already exists from a prior `docker compose up`
-- (the CREATE TABLE IF NOT EXISTS above is then a no-op), so the columns are still added
-- idempotently on re-run.
SET @rewrite_model_exists = (
  SELECT COUNT(*) FROM information_schema.columns
  WHERE table_schema = 'pipeline' AND table_name = 'jobs' AND column_name = 'rewrite_model'
);
SET @add_rewrite_model = IF(@rewrite_model_exists = 0,
  'ALTER TABLE `jobs` ADD COLUMN `rewrite_model` VARCHAR(128) NULL AFTER `published_post_url`',
  'SELECT 1');
PREPARE stmt FROM @add_rewrite_model;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @generated_at_exists = (
  SELECT COUNT(*) FROM information_schema.columns
  WHERE table_schema = 'pipeline' AND table_name = 'jobs' AND column_name = 'generated_at_utc'
);
SET @add_generated_at = IF(@generated_at_exists = 0,
  'ALTER TABLE `jobs` ADD COLUMN `generated_at_utc` DATETIME NULL AFTER `rewrite_model`',
  'SELECT 1');
PREPARE stmt FROM @add_generated_at;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
