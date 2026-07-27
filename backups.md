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

docker exec $(docker ps --filter "name=orbi-stack_orbiapp" --format "{{.ID}}") ls -lh /backups/
