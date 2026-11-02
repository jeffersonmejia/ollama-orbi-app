# Línea base de auditoría

- Fecha/hora local: 2026-11-01 10:52:02 -05:00 (America/Guayaquil)
- Rama creada para auditoría: audit/orbi-sonar-zap-20261101
- Commit inicial: 153f8a59e98debb421465db9455bf4d11d76c21d
- Estado inicial: el worktree ya contenía cambios sin commit en README, solución y presentación Blazor/MVC; no se descartaron ni mezclaron esos cambios.
- Plataforma observada: Windows x64, Docker Engine 29.8.0, .NET SDK 10.0.403; runtimes Microsoft.NETCore.App y Microsoft.AspNetCore.App 10.0.14.
- Solución efectiva: OrbiApp.slnx; proyectos: Domain, Application, Infrastructure, Presentation.Client, Presentation y la herramienta OrbiApp.DataGenerator. No se hallaron proyectos *Test*.csproj.
- dotnet restore OrbiApp.slnx: éxito (exit 0).
- dotnet build OrbiApp.slnx --no-restore: éxito, 0 errores y 6 avisos NuGet (NU1901, NU1902, NU1903); detalle en ../evidencias/aplicacion/dotnet-build-baseline.log.
- Docker Compose: orbi-postgres, postgres:16-alpine, healthy; host 127.0.0.1:5433 -> contenedor 5432. Docker Swarm: inactive, nodo no es manager.
- Aplicación existente: HTTP http://localhost:5175, proceso OrbiApp.Presentation.exe; se dejó en ejecución. No se inició de nuevo ni se detuvo.
- HTTP no autenticado observado: / 200; /Identity/Account/Login 200; /Identity/Account/Register 200; /app/asistente 200; /errors/404 404; /api/security/antiforgery 200. Ver ../evidencias/aplicacion/pruebas-funcionales.log.
- La API explícita observada en la configuración es GET /api/security/antiforgery; el resto se publica principalmente por ruta MVC convencional y Razor Pages de Identity. Inventario de atributos de controladores: ../evidencias/aplicacion/route-attributes.csv.
- Escaneo autenticado no realizado: no se suministraron credenciales de prueba para cliente, vendedor, repartidor o administrador. No se probaron operaciones de negocio, pagos, webhooks, correo, backups ni restauración.
- Servicio Ollama escucha localmente en 127.0.0.1:11434; la aplicación configura modelo qwen2.5:0.5b. No se envió una consulta al modelo.
- Configurados en el código: PostgreSQL/Npgsql; Ollama HTTP; PayPal vía HttpClient; consulta de precios externos (Fybeca, Pharmacys y Medicity); SMTP con MailKit; EmailQueueWorker y BackupService. No se contactaron pasarelas, tiendas, SMTP ni se ejecutaron trabajos manuales.
- PayPhone aparece en modelos/migraciones históricas de pagos; se preservó sin alteraciones por el alcance de auditoría. En la inspección funcional actual no se dispararon flujos de pago.
- Headers observados: las respuestas de login y asistente incluyen X-Frame-Options; la página del asistente incluye Content-Security-Policy. La portada no mostró esos headers en la revisión inicial. Captura de nombres (sin cookies/valores) pendiente de registrar en evidencias ZAP.
- SonarQube Community Build 26.10.0.133078 quedó operativo en 127.0.0.1:9001, aislado y con volúmenes exclusivos. SonarScanner for .NET 12.0 completó la secuencia begin/build/end; tarea 431baa9e-8c4f-4612-97c1-68f7e99fd76a SUCCESS, Quality Gate OK. Métricas e issues: sonarqube-antes.json.
- OWASP ZAP 2.17.0 ejecutó Baseline contra http://host.docker.internal:5175 con contexto limitado; 42 URLs y 12 alert objects (8 con riesgo medio/bajo y 4 informativos), 6 categorías WARN en salida CLI. Solo spider/pasivo; sin active scan. Reportes: zap-antes.json/html.

## Resultado del análisis estático y dinámico

- SonarQube: análisis ejecutado contra `OrbiApp.slnx` sobre el worktree actual, en `Release`; 161 archivos procesados y exclusiones solo para `auditoria/**`, binarios/carpetas generadas y bibliotecas web de terceros. El análisis SCM se desactivó después de un error Git `MissingObjectException`; no se excluyó código propio.
- SonarQube informó 211 issues abiertos en el alcance: 53 bugs, 14 vulnerabilidades y 144 code smells; 0 Security Hotspots; 19 266 líneas de código; 0,7 % de líneas duplicadas; 0,0 en el API de coverage sin importar reporte de cobertura. Quality Gate: `OK` según la instancia/perfil local.
- ZAP Baseline descubrió 42 URLs y ejecutó análisis pasivo; no hubo alertas `FAIL`, hubo seis tipos `WARN-NEW` y cuatro informativas en el JSON. Se guardó evidencia anonimizada. No se ejecutó active scan ni autenticación.
- El build instrumentado `Debug` no pudo escribir sobre los ensamblados en uso por la app de usuario (MSB3027/MSB3021). La secuencia Sonar se repitió en `Release` y completó correctamente.
- Primera secuencia ZAP falló antes de iniciar spider por falta de slash final en la coincidencia del contexto. Se corrigió el regex y la ejecución aceptada terminó con exit 0.
- Se seleccionó una sola corrección de ZAP: alert 10021, `X-Content-Type-Options Header Missing`, Low (Medium), en 5 respuestas. La evidencia anterior está en `evidencias/mejora/cabecera-10021-antes.json`.
- Después de esa corrección, `dotnet build OrbiApp.slnx --configuration Release --no-incremental` terminó con exit 0, 0 errores y 14 advertencias. El proceso de usuario en `localhost:5175` sigue ejecutando el ensamblado Debug anterior; por la instrucción previa del usuario de ejecutar personalmente el backend, no se detuvo ni reinició. Quedan pendientes la comprobación HTTP del nuevo header y el ZAP posterior sobre la app actualizada.- El servidor Sonar informa `Quality Gate: OK`; la condición devuelta fue `new_violations = 0`. Las 211 issues existentes siguen registradas y no son anuladas por ese gate.
- El proyecto SonarQube informa la rama de servidor `main`; el scanner analizó el worktree del repositorio en `audit/orbi-sonar-zap-20261101`.
- Los workers de email y backup estaban registrados en el proceso web preexistente. No se invocaron manualmente; no se revisó si completaron ciclos automáticos antes o durante la auditoría.
- El Compose presente define únicamente el servicio PostgreSQL; no se hallaron Dockerfile ni docker-stack.yml/.yaml. La app observada corre como proceso local; Swarm estaba inactivo.
