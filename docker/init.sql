-- docker/init.sql
-- Runs once on container first boot via docker-entrypoint-initdb.d
-- Creates the unaccent extension (idempotent — safe even if the EF Core migration also runs it)
CREATE EXTENSION IF NOT EXISTS unaccent;
