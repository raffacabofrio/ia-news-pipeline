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
  `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`job_id`),
  INDEX `ix_jobs_state_created_at` (`state`, `created_at`)
);
