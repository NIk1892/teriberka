#!/bin/bash
set -e

# Схему public указываем явно: без неё расширение уехало бы в search_path сессии.
# CRLF в этом файле ломает shebang в Linux-контейнере («required file not found»).
psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "CREATE EXTENSION IF NOT EXISTS citext WITH SCHEMA public;"
psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA public;"
psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "CREATE EXTENSION IF NOT EXISTS btree_gin WITH SCHEMA public;"
