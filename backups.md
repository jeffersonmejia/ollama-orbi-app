# Backups — Orbi App

## How it works

`BackupService` is a background service that runs `pg_dump` automatically on a configurable interval. Backups are stored on the host machine via a Docker bind mount (`./backups` -> `/backups` in the container).

## Configuration

| Variable | Default | Description |
|---|---|---|
| `BACKUP_DIR` | `/backups` | Directory inside the container where `.sql` files are written |
| `BACKUP_INTERVAL_MINUTES` | `5` | Minutes between each backup |
| `BACKUP_RETENTION_COUNT` | `12` | Maximum number of backup files to keep (oldest are deleted) |

These values are set as environment variables in `docker-stack.yml` under the `orbiapp` service. To change them, edit the stack file and re-deploy.

## File naming

Backups are saved as:

```
orbi_backup_YYYYMMDD_HHMMSS.sql
```

Example: `orbi_backup_20260726_223058.sql`

## Host directory

Backups are persisted in the `./backups` directory on the Docker host. This directory is bind-mounted into the container so backups survive container restarts and redeployments.

To list backups on the host:

```bash
ls -lh ./backups/
```

## Dashboard

The admin dashboard (`/Home/Index`) shows a countdown bar indicating when the next automatic backup will run. During a backup the bar turns blue and shows a spinning icon.

## Manual backup

To create a manual backup from the host:

```bash
docker exec <container_name> pg_dump -h postgres -p 5432 -U cruduser -d orbi_app --no-owner --no-privileges > ./backups/manual_backup.sql
```

## Restoring a backup

```bash
docker exec -i <container_name> psql -h postgres -p 5432 -U cruduser -d orbi_app < ./backups/orbi_backup_YYYYMMDD_HHMMSS.sql
```
