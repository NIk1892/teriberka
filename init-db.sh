#!/bin/bash
set -e

psql -U "$POSTGRES_USER" -c "CREATE DATABASE tb_db;"
psql -U "$POSTGRES_USER" -d tb_db -c "CREATE EXTENSION IF NOT EXISTS citext;"
