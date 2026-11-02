# Pruebas de Orbi

Este archivo local está excluido de Git por `/test.md` en `.gitignore`.

## Estructura por tipo

```text
tests/
├── Contract/                 15 casos; atributos, rutas, roles, validación y contratos
├── Integration/              5 casos; consultas contra PostgreSQL aislado
│   ├── Catalog/
│   ├── CommerceIdentity/
│   ├── Operations/
│   ├── Schema/
│   └── Shared/                fixture y consultas reutilizables
└── Performance/              1 caso original PostgreSQL + 1 caso HTTP nuevo
    ├── Database/              conserva DatabasePerformanceTests
    ├── Reports/               JSON y Markdown producidos por ejecuciones reales
    ├── HttpTestHost.cs        WebApplicationFactory, auth de prueba y datos aislados
    └── PerformanceRunnerProgram.cs
```

No hay casos Unit en el conjunto original. Los 15 casos Contract no se cuentan como integración HTTP. Los cinco casos Integration ejecutan EF Core contra PostgreSQL. Se mantienen los 21 casos originales: 15 Contract, 5 Integration y 1 Performance de base de datos. El caso HTTP nuevo se añade a Performance; sus 7 escenarios HTTP cubren catálogo con resultados y paginación, pedidos y pagos existentes/vacíos/paginados y operaciones.

## PostgreSQL aislado

El fixture solo admite `127.0.0.1:55433`, base `orbi_audit`, rol `orbi_app`. Rechaza otras conexiones. Aplica migraciones y scripts, genera 40.000 productos temporales con prefijo aleatorio, y limpia sus datos al terminar. Las pruebas HTTP agregan 13 pedidos y pagos asociados al prefijo y también los eliminan. Nunca apuntes estas variables a una base de producción.

Prepara el contenedor local (secrets permanecen dentro de variables de entorno):

```powershell
docker compose -p orbi-audit-test -f auditoria/postgres-audit-test.compose.yml up -d
$dbTestPassword = [IO.File]::ReadAllText('.docker/secrets/orbi-audit-test-postgres-app-password').Trim()
$env:ORBI_DATABASE_TEST_CONNECTION = "Host=127.0.0.1;Port=55433;Database=orbi_audit;Username=orbi_app;Password=$dbTestPassword;Timeout=30"
```

## Comandos por categoría

```powershell
# Unit: no hay casos Unit en el inventario actual
# dotnet test tests/Unit/OrbiApp.Unit.Tests.csproj

dotnet test tests/Integration/OrbiApp.Integration.Tests.csproj --configuration Release
dotnet test tests/Contract/OrbiApp.Contract.Tests.csproj --configuration Release

# Rendimiento PostgreSQL conservado: 3 calentamientos, 30 muestras y referencia existente p95 < 500 ms
dotnet test tests/Performance/OrbiApp.Performance.Tests.csproj --configuration Release --filter "FullyQualifiedName~DatabasePerformanceTests"

# Rendimiento HTTP WebApplicationFactory: 3 calentamientos y 30 muestras por escenario
$env:ORBI_PERFORMANCE_ENABLED = '1'
dotnet test tests/Performance/OrbiApp.Performance.Tests.csproj --configuration Release --filter "Category=Performance&FullyQualifiedName~HttpPerformanceTests"

# Carga manual: 1, 10, 25 y 50 usuarios virtuales; 30 s por escenario por defecto
$env:ORBI_LOAD_TEST_ENABLED = '1'
$env:ORBI_LOAD_DURATION_SECONDS = '30'   # permitido: 1..300
$env:ORBI_LOAD_MAX_REQUESTS = '100000'   # permitido: 1..250000 por escenario
dotnet run --project tests/Performance/OrbiApp.Performance.Tests.csproj --configuration Release -- load

# Estrés manual: límites de latencia y error son obligatorios y los define quien ejecuta
$env:ORBI_STRESS_MAX_P95_MS = Read-Host 'Define P95 máximo en milisegundos'
$env:ORBI_STRESS_MAX_ERROR_PERCENT = Read-Host 'Define porcentaje máximo de errores (0..100)'
$env:ORBI_STRESS_RECOVERY_SECONDS = '10' # permitido: 1..120
dotnet run --project tests/Performance/OrbiApp.Performance.Tests.csproj --configuration Release -- stress

Remove-Item Env:ORBI_PERFORMANCE_ENABLED,Env:ORBI_LOAD_TEST_ENABLED,Env:ORBI_LOAD_DURATION_SECONDS,Env:ORBI_LOAD_MAX_REQUESTS,Env:ORBI_STRESS_MAX_P95_MS,Env:ORBI_STRESS_MAX_ERROR_PERCENT,Env:ORBI_STRESS_RECOVERY_SECONDS,Env:ORBI_DATABASE_TEST_CONNECTION
docker compose -p orbi-audit-test -f auditoria/postgres-audit-test.compose.yml stop
```

Los subcomandos `load` y `stress` usan `WebApplicationFactory`/`TestServer`; no admiten una URL pública. El harness verifica la misma allowlist de PostgreSQL del fixture, ejecuta migraciones/semilla antes de iniciar relojes y limita duración, concurrencia, solicitudes y timeout. Cada operación HTTP genera su propio scope backend/DbContext; el `HttpClient` es thread-safe y no comparte contextos EF. El estrés aumenta 10, 25, 50, 100 y 200 usuarios virtuales, se detiene ante cinco fallos consecutivos o al superar los límites de degradación y termina con una ventana de recuperación a concurrencia 1.

Las pruebas HTTP validan estado y contenido. Las pruebas de carga registran códigos HTTP (incluidos los errores de transporte), requests/s, porcentaje de error y latencia. Los datos de ejecución aparecen en `tests/Performance/Reports/performance-results.json` y `.md`; no se guardan resultados ficticios ni umbrales de aceptación predeterminados. No ejecutes `stress` en CI ordinario.

## Recuento original y comandos de descubrimiento

Conteo de casos xUnit originales: 21 (las cuatro filas de `Cart_quantity_contract_enforces_supported_range` cuentan como cuatro casos). El caso HTTP de rendimiento es nuevo; carga/estrés son comandos manuales y no casos xUnit.

```powershell
dotnet build OrbiApp.slnx --configuration Release
dotnet test tests/Contract/OrbiApp.Contract.Tests.csproj --configuration Release --list-tests
dotnet test tests/Integration/OrbiApp.Integration.Tests.csproj --configuration Release --list-tests
dotnet test tests/Performance/OrbiApp.Performance.Tests.csproj --configuration Release --list-tests
```

