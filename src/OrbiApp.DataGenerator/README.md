# OrbiApp.DataGenerator

Generador independiente de datos realistas para el esquema de `src/db`. Usa Bogus
con locale español y `NpgsqlBinaryImporter` para insertar lotes transaccionales sin
mantener el millón de registros completo en memoria.

## Preparación

La aplicación web debe haber ejecutado primero sus migraciones de Identity. El
generador aplica de forma idempotente `orbi-schema.sql` y `orbi-locations.sql`, pero
no crea ni modifica credenciales de conexión.

## Ejecución

Desde la raíz del repositorio:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5432;Database=orbi_app;Username=USUARIO;Password=CLAVE'
dotnet run --project src/OrbiApp.DataGenerator -- --records 1000000 --batch-size 5000 --seed 2026 --reset
```

`--reset` es explícito y destructivo para las tablas de negocio. Sin esa opción el
generador se detiene si detecta registros, evitando una carga duplicada accidental.

La precedencia de configuración es: parámetros de línea de comandos, variables de
entorno y `src/appsettings.json`. Variables disponibles:

- `ConnectionStrings__DefaultConnection`
- `DATA_GENERATION_RECORDS`
- `DATA_GENERATION_BATCH_SIZE`
- `DATA_GENERATION_SEED`
- `DATA_GENERATION_LOCALE`

La fecha ancla predeterminada es fija para que dos bases vacías, con igual semilla y
configuración, reciban datos equivalentes. Puede cambiarse con `--reference-date`.

## Validación

El comando termina ejecutando validaciones de cantidad, unicidad, ubicación,
relaciones, subtotales y totales. La misma comprobación puede repetirse con:

```powershell
psql "$env:ConnectionStrings__DefaultConnection" -f src/db/validate-generated-data.sql
```

La distribución predeterminada suma exactamente 1.000.000 de registros:

| Tabla | Registros |
|---|---:|
| delivery_store | 2.000 |
| delivery_product | 80.000 |
| user_profile | 120.000 |
| delivery_order | 240.000 |
| delivery_order_item | 420.000 |
| payment | 90.000 |
| inventory_movement | 35.000 |
| audit_log | 10.000 |
| delivery_incident | 3.000 |
