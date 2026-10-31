#!/bin/sh

app_password="$(cat /run/secrets/orbi_app_password)"

psql \
  --username "$POSTGRES_USER" \
  --dbname "$POSTGRES_DB" \
  --no-password \
  --set=ON_ERROR_STOP=1 \
  --set=app_password="$app_password" \
  --set=database="$POSTGRES_DB" <<'SQL'
SELECT format(
    'CREATE ROLE orbi_app WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD %L',
    :'app_password'
)
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'orbi_app')
\gexec

SELECT format(
    'ALTER ROLE orbi_app WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD %L',
    :'app_password'
)
\gexec

GRANT CONNECT ON DATABASE :"database" TO orbi_app;
GRANT USAGE, CREATE ON SCHEMA public TO orbi_app;
ALTER SCHEMA public OWNER TO orbi_app;

DO $$
DECLARE
    object_record record;
BEGIN
    FOR object_record IN
        SELECT namespace.nspname AS schema_name, relation.relname AS object_name, relation.relkind
        FROM pg_class AS relation
        JOIN pg_namespace AS namespace ON namespace.oid = relation.relnamespace
        WHERE namespace.nspname = 'public'
          AND relation.relkind IN ('r', 'p', 'S', 'v', 'm')
    LOOP
        CASE object_record.relkind
            WHEN 'S' THEN
                EXECUTE format('ALTER SEQUENCE %I.%I OWNER TO orbi_app', object_record.schema_name, object_record.object_name);
            WHEN 'v' THEN
                EXECUTE format('ALTER VIEW %I.%I OWNER TO orbi_app', object_record.schema_name, object_record.object_name);
            WHEN 'm' THEN
                EXECUTE format('ALTER MATERIALIZED VIEW %I.%I OWNER TO orbi_app', object_record.schema_name, object_record.object_name);
            ELSE
                EXECUTE format('ALTER TABLE %I.%I OWNER TO orbi_app', object_record.schema_name, object_record.object_name);
        END CASE;
    END LOOP;
END
$$;
SQL

unset app_password
