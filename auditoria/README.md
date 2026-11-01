# Artefactos técnicos de auditoría

- Estado base y observaciones del entorno: `resultados/00-linea-base.md`.
- Componentes y dependencias distribuidas: `resultados/inventario-componentes.json`.
- Rutas y permisos observados: `resultados/endpoints.json`.
- Diagrama: `resultados/arquitectura.mmd`.
- SonarQube: `resultados/sonarqube-antes.json`; logs en `evidencias/sonarqube/`.
- ZAP Baseline: JSON/HTML en `resultados/` y `evidencias/zap/`.
- Issues de Sonar y alertas de ZAP: `resultados/hallazgos-tecnicos.csv`.
- Corrección única: alert 10021 (`X-Content-Type-Options Header Missing`); snapshot antes en `evidencias/mejora/` y diff en `resultados/mejora-unica.diff`.
- Build y smoke: `resultados/pruebas-funcionales.log`; comparación: `resultados/comparacion-antes-despues.csv`.
- Limitaciones: `resultados/limitaciones-tecnicas.txt`.

## Comandos ejecutados

Desde la raíz del repositorio, en PowerShell:

```powershell
$env:SONAR_TOKEN = Get-Content .docker/secrets/orbi_sonar_token -Raw
& "$env:USERPROFILE\.dotnet\tools\dotnet-sonarscanner.exe" begin /k:orbi-auditoria /n:"Orbi - Auditoria" /d:sonar.host.url=http://127.0.0.1:9001 /d:sonar.token="$env:SONAR_TOKEN" /d:sonar.scm.disabled=true '/d:sonar.exclusions=auditoria/**,**/bin/**,**/obj/**,**/publish/**,**/node_modules/**,**/packages/**,**/wwwroot/lib/**,**/wwwroot/_framework/**'
& "C:\Program Files\dotnet\dotnet.exe" build OrbiApp.slnx --configuration Release --no-incremental
& "$env:USERPROFILE\.dotnet\tools\dotnet-sonarscanner.exe" end /d:sonar.token="$env:SONAR_TOKEN"
$mount = (Join-Path $PWD 'auditoria/evidencias/zap').Replace('\', '/') + ':/zap/wrk:rw'
docker run --rm -v $mount ghcr.io/zaproxy/zaproxy:stable zap-baseline.py -t http://host.docker.internal:5175 -n /zap/wrk/orbi-local.context -m 1 -r zap-antes.html -J zap-antes.json -I
```

La instancia de SonarQube está aislada en `127.0.0.1:9001`, con volúmenes `orbi-sonarqube-audit-*`. El token y la contraseña aleatoria de su administrador están solo en `.docker/secrets/`, excluidos de Git. La ejecución aceptada analizó `OrbiApp.slnx`; una pasada inicial que incluyó resultados de auditoría se conserva como diagnóstico y no se usa como baseline.

Revalidación de solo lectura contra la API local de SonarQube: servidor `UP`, tarea `431baa9e-8c4f-4612-97c1-68f7e99fd76a` `SUCCESS`, rama `main`, gate `OK` por `new_violations=0`; 211 issues no resueltos y 75 resueltos. Datos y fecha: `evidencias/sonarqube/revalidacion-20261101.json`.

## Verificación posterior pendiente

La corrección compila, pero `localhost:5175` sigue cargando el binario Debug anterior. ZAP se repitió contra ese proceso y la alerta permanece. Para verificar en aislamiento, se preparó y arrancó `postgres-audit-test.compose.yml`: PostgreSQL está `healthy` en `127.0.0.1:55433`, la base `orbi_audit` está vacía, el rol `orbi_app` no es superusuario y el volumen es exclusivo. Los secretos son aleatorios, locales e ignorados por Git. No se inició un backend adicional. Evidencia: `evidencias/aplicacion/postgres-audit-test.json`.

Desde la raíz, el usuario puede iniciar la base de auditoría:

```powershell
docker compose -p orbi-audit-test -f .\auditoria\postgres-audit-test.compose.yml up -d
docker compose -p orbi-audit-test -f .\auditoria\postgres-audit-test.compose.yml ps
```

En otra consola PowerShell, iniciar el backend exclusivamente contra esa base y por el puerto `5176`. El usuario mantiene su backend actual en `5175`. El directorio de backups se hace único y el intervalo queda en 24 horas; el SMTP se limita a loopback:

```powershell
$auditDbPassword = (Get-Content .docker\secrets\orbi-audit-test-postgres-app-password -Raw).Trim()
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=55433;Database=orbi_audit;Username=orbi_app;Password=$auditDbPassword"
$env:BACKUP_DIR = Join-Path $env:TEMP ("orbi-audit-test-backups-" + [guid]::NewGuid().ToString('N'))
$env:BACKUP_INTERVAL_MINUTES = '1440'
$env:BACKUP_RETENTION_COUNT = '12'
$env:EmailSettings__SmtpServer = '127.0.0.1'
$env:EmailSettings__SmtpPort = '1'
$env:EmailSettings__SenderName = 'Orbi Audit'
$env:EmailSettings__SenderEmail = 'audit@localhost'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
& "C:\Program Files\dotnet\dotnet.exe" watch --project src\Core\Presentation\Presentation.csproj run --configuration Release --no-launch-profile -- --urls "http://localhost:5176"
```

Cuando ese backend responda, repetir la comprobación ZAP pasiva. El contexto excluye Registro completo porque el spider envió POST de formulario en la primera pasada; esa ruta quedará fuera de la cobertura posterior:

```powershell
$mount = (Join-Path $PWD 'auditoria/evidencias/zap').Replace('\', '/') + ':/zap/wrk:rw'
docker run --rm -v $mount ghcr.io/zaproxy/zaproxy:stable zap-baseline.py -t http://host.docker.internal:5176 -n /zap/wrk/orbi-audit-post.context -m 1 -r zap-despues-aislado.html -J zap-despues-aislado.json -I
```

Después de la prueba, detener el backend aislado con `Ctrl+C` y luego detener solo la base temporal, conservando su volumen mientras no se haya revisado el resultado:

```powershell
docker compose -p orbi-audit-test -f .\auditoria\postgres-audit-test.compose.yml stop
```

La instancia original no se detiene ni reinicia. La ruta `/Identity/Account/Register` no se evalúa en el reporte posterior por la exclusión preventiva; el resto del contexto local mantiene las exclusiones de pagos, operaciones, inventario, APIs no antiforgery y rutas administrativas.

ZAP se mantuvo pasivo y restringido al host/puerto local. No se hicieron pruebas autenticadas, pagos, envío de correo, operaciones de negocio ni active scan. PostgreSQL original continúa enlazado solo a `127.0.0.1:5433`.

Revalidación HTTP del 2026-11-01 16:34 UTC: las rutas públicas y el endpoint antiforgery respondieron (200; ruta inexistente 404). El proceso que atiende `5175` es el binario Debug preexistente; `X-Content-Type-Options` sigue ausente. Evidencia: `evidencias/aplicacion/smoke-revalidacion-20261101.json`.

No se reinició el backend para no alterar datos: `Program.cs` invoca `DatabaseInitializer.InitializeAsync` en cada inicio. `orbi-schema.sql` cambia a `PayPal` las preferencias de pago distintas y `IdentitySeeder` reconcilia roles, perfiles y direcciones de cuentas demo. La verificación actualizada se hará en la base desechable preparada, sin cambiar la base original.

El reporte ZAP contiene `POST /Identity/Account/Register` generado por el spider de formulario (valores de prueba como `Input.Cedula=ZAP`). La validación del modelo invalida esa cédula y el flujo solo llama a `CreateAsync` cuando `ModelState.IsValid`; no se verificó el estado de la base independientemente. Para la futura pasada, usar `orbi-audit-post.context`, que excluye Registro entero para impedir nuevos POST del spider; esto reduce esa cobertura respecto al baseline.
Revalidación de compilación 2026-11-01T16:47:16Z UTC: SDK .NET 10.0.403; build Release de OrbiApp.slnx con exit code 0, 14 advertencias y 0 errores. Log sanitizado: evidencias/aplicacion/dotnet-build-revalidacion-20261101.log.
Comprobación de disponibilidad para la pasada posterior, 2026-11-01T16:49:58Z: `evidencias/aplicacion/runtime-readiness-20261101.json` (5175 responde 200 sin X-Content-Type-Options; 5176 no acepta conexión; Postgres temporal healthy).
