# ORBI — INSTRUCCIONES PARA CODEX: AUDITORÍA TÉCNICA CON SONARQUBE Y OWASP ZAP

## Rol y objetivo
Actúa como auditor técnico de una aplicación distribuida ASP.NET Core .NET 10 llamada **Orbi**. Trabaja en el repositorio existente y ejecuta la auditoría, no te limites a proponer pasos. El objetivo es producir evidencia verificable de calidad y seguridad, implementar **una sola mejora** y comprobar que las funcionalidades principales siguen operativas. **Alcance exclusivo: ejecutar herramientas, verificar el sistema, aplicar una corrección y guardar resultados técnicos verificables. No redactes informes, secciones académicas, respuestas teóricas, discusión, conclusiones ni recomendaciones para un documento.**

No inventes resultados, alertas, métricas, rutas, archivos ni capturas. No introduzcas vulnerabilidades para que aparezcan hallazgos. Diferencia resultados confirmados, hipótesis y verificaciones no realizadas. Si falta una herramienta, credencial o permiso, prepara cuanto sea posible y registra con exactitud el bloqueo y la instrucción mínima para resolverlo.

## Contexto funcional conocido: verificar contra código
- Aplicación comercial para Ecuador, con roles cliente, vendedor, repartidor y administrador.
- ASP.NET Core MVC, Razor Pages, C#, JavaScript, CSS, .NET 10, EF Core, Identity, PostgreSQL 18 y Npgsql.
- Docker Swarm con servicios `orbiapp` y `postgres`, red overlay y Docker Secrets.
- Puerto web declarado: `5164`; puerto PostgreSQL declarado: `5433`. Comprueba realmente interfaces de enlace y exposición externa.
- Autenticación con Google, MFA TOTP, correo SMTP con MailKit y trabajo en segundo plano.
- Pagos con PayPal Sandbox y PayPhone: no realizar cobros ni alterar transacciones.
- Servicio Ollama vía HTTP para chat, sugerencias y autocompletado; comprobar controles de acceso y minimización de datos.
- Servicios de backups con `pg_dump`, restauración con `psql`, seeding y generador masivo de datos.
- Proyecto principal conocido `src/SakilaApp.csproj`, pero detecta la solución y proyectos efectivos antes de ejecutar comandos.

El archivo de ejemplo `sonar-project.properties` del usuario contiene `sonar.projectKey=mini-helpdesk-bad-practices`, `sonar.projectName=Mini Help Desk - Bad Practices`, `sonar.sources=src,public` y exclusiones de `node_modules`. **NO** uses esas identidades ni el enfoque de scanner genérico para analizar C#. Crea una configuración aislada para Orbi empleando **SonarScanner for .NET** y su secuencia `begin -> dotnet build -> end`. Usa, por ejemplo, el identificador `orbi-auditoria`, sujeto a comprobar que no colisiona con un proyecto previo.

## Restricciones obligatorias
- Trabaja solo sobre recursos propios del usuario, instancia local o entorno de pruebas explícitamente autorizado.
- Inspecciona y registra el estado inicial, rama, commit, servicios, configuración relevante y ruta de aplicación; evita mostrar secretos en salidas o evidencias.
- Antes de tocar código crea una rama de auditoría cuando sea posible y registra el commit inicial; no sobrescribas cambios ajenos sin autorización.
- **Prohibido** ejecutar `--reset`, borrado de datos, reinicio de esquema, restauración de producción, eliminación de volúmenes, recreación destructiva de stack o generación masiva de registros. Evita activar seeders que cambien datos de negocio sin revisar sus efectos.
- No escanees activamente endpoints de pago, webhooks, envío de correo, administración, restauración, backups, borrado ni operaciones de escritura sensibles. Para escaneo activo habilita únicamente una instancia de laboratorio con datos ficticios, respaldo validado y alcance restringido. El análisis pasivo tiene prioridad.
- No utilices `docker compose down -v`, `docker system prune`, `docker stack rm` ni equivalentes destructivos sin autorización expresa.
- Si existen credenciales para clientes, vendedores, repartidores y administradores, úsalas únicamente de forma segura y sin registrarlas; si no existen, documenta cobertura no autenticada y solicita el acceso necesario.
- No cambies la lógica de pagos, Identity, MFA, autorización, migraciones, datos existentes ni estructura de base de datos como parte de la única mejora, salvo que el hallazgo seleccionado lo requiera y se pueda probar rigurosamente.
- La evidencia de un hallazgo debe proceder del resultado de una herramienta o una reproducción técnica verificable, nunca de una mera suposición.
- No persigas un Quality Gate aprobado artificialmente; documenta su estado real y las condiciones del perfil utilizado.

## Fase A — Reconocimiento y línea base

1. Examina el repositorio: estructura, proyectos `.sln`/`.csproj`, `Program.cs`, controladores, servicios, vistas, JavaScript, configuración, Dockerfile y `docker-stack.yml`.
2. Crea `auditoria/evidencias/` y `auditoria/resultados/` y excluye de Git cualquier informe con cookies, tokens, datos personales o secretos. No introduzcas credenciales en archivos versionados.
3. Verifica herramientas: `dotnet --info`, `dotnet restore`, `dotnet build`, Docker, estado Swarm, servicios y puertos. Reporta errores exactos.
4. Ejecuta pruebas existentes si las hay. Registra resultados y cobertura disponible, sin presentar la ausencia de pruebas como cobertura cero medida.
5. Verifica la aplicación mediante respuestas HTTP y, si es posible, navegación visual; comprueba inicio, autenticación y funciones representativas por rol sin modificar datos importantes.
6. Elabora un inventario **comprobado** de componentes, puertos, rutas, endpoints, protocolos, dependencias y conexiones. Distingue rutas que requieren sesión, roles y permisos.
7. Elabora un diagrama Mermaid de arquitectura basado en el despliegue observado, que refleje navegador -> ASP.NET Core -> PostgreSQL, integraciones externas, Ollama y trabajos en segundo plano. No inventes microservicios.
8. Guarda la línea base en `auditoria/resultados/00-linea-base.md`: fecha/hora, host local sin información privada, commit, herramientas/versiones, estados de servicios, pruebas y URLs verificadas.

## Fase B — SonarQube: análisis estático

1. Comprueba si SonarQube Server/Community Build ya está disponible; si no, ofrece despliegue local no destructivo aislado, con puerto y persistencia sin colisión. Usa credenciales y token obtenidos de forma segura; nunca imprimas tokens completos.
2. Usa **SonarScanner for .NET** compatible con la versión del servidor y del SDK. Si falta, instálalo como herramienta local o global según el entorno; registra versión.
3. Ejemplo conceptual en PowerShell (ajusta ruta y autenticación al entorno):

```powershell
# Configurar $env:SONAR_TOKEN con un secreto obtenido de forma segura.
dotnet tool install --global dotnet-sonarscanner

dotnet sonarscanner begin /k:"orbi-auditoria" /n:"Orbi - Auditoria" /d:sonar.host.url="http://localhost:9000" /d:sonar.token="$env:SONAR_TOKEN"
dotnet build .\src\SakilaApp.csproj --no-incremental
dotnet sonarscanner end /d:sonar.token="$env:SONAR_TOKEN"
```

Si SonarScanner ya está instalado, no repitas la instalación. Detecta si la compilación debe realizarse con la solución, con propiedades de entorno, proyecto de pruebas u otros ajustes. No uses `sonar.sources` como sustituto del análisis de C#. Excluye carpetas generadas, backups, paquetes, binarios y archivos de terceros según corresponda; **no excluyas código propio para ocultar issues**.

4. Espera a que SonarQube termine de procesar el análisis antes de consultar resultados. Guarda ID de tarea, URL del dashboard, versión, fecha y commit.
5. Extrae de la interfaz o API, si hay acceso autorizado: Maintainability, Reliability, Security, Duplications, Issues, Quality Gate, ratings y métricas disponibles. Registra para cada métrica denominación, unidad, alcance y valor. No inventes Security Hotspots, cobertura ni líneas duplicadas si la edición no las informa.
6. Registra **al menos tres issues confirmados y relevantes**, preferiblemente distintos en categoría o componente, sin alterar los resultados. Para cada uno: ID, regla, severidad, tipo/categoría, archivo, línea, descripción de la regla, contexto del código, riesgo verificable, impacto en el sistema distribuido y recomendación concreta.
7. Si el escaneo devuelve menos de tres hallazgos, revisa configuración, cobertura legítima del análisis y alcance; informa el resultado sin fabricar problemas ni debilitar el sistema.
8. Conserva capturas de dashboard e issues, exportaciones permitidas o JSON sanitizado de API en `auditoria/evidencias/sonarqube/` y `auditoria/resultados/sonarqube-antes.*`. Los nombres deben identificar claramente evidencia y fase.

## Fase C — OWASP ZAP: auditoría dinámica

1. Determina URL exacta y accesibilidad desde ZAP. Si ZAP se ejecuta en contenedor, comprueba la resolución de `host.docker.internal` en Docker Desktop y la conectividad; `localhost` dentro del contenedor no es automáticamente el host. Usa la URL realmente accesible.
2. Define un **Context** y un alcance de escaneo limitado al host, puerto y rutas de laboratorio. Excluye dominios externos, pasarelas de pago, enlaces de logout, mutaciones sensibles, archivos de backup, restauración y acciones administrativas destructivas. No ataques servicios externos.
3. Ejecuta primero spider y **passive scan**; usa la interfaz de ZAP o su imagen oficial. Ejemplo de referencia, solo después de comprobar conectividad y ajustar la carpeta compartida:

```powershell
# Ejemplo de escaneo no autenticado y pasivo en aplicación local.
# Ajustar la URL para que sea alcanzable desde el contenedor.
docker run --rm -v "${PWD}/auditoria/evidencias/zap:/zap/wrk:rw" ghcr.io/zaproxy/zaproxy:stable zap-baseline.py -t http://host.docker.internal:5164 -r zap-antes.html -J zap-antes.json -I
```

4. Para las rutas autenticadas, configurar sesiones, usuarios y verificación de autenticación en ZAP si es viable. No asumir que el spider sin autenticar cubre inventario ni administración.
5. Ejecuta active scan **solo** si existe una instancia desechable y autorizada, sin datos importantes, con alcance verificado y exclusiones validadas. Documenta si se omitió y por qué. Las pruebas activas pueden generar solicitudes que modifican datos.
6. Selecciona **al menos tres alertas realmente detectadas**, con: nombre exacto, ID/plugin ID, riesgo, confianza, URL/método/endpoint afectado, evidencia sanitizada, explicación contextual, impacto en comunicación entre componentes y corrección recomendada. Prioriza alertas de riesgo alto o medio verificadas; si no aparecen, reporta niveles disponibles sin aumentar artificialmente su severidad.
7. Analiza falsas alarmas: confirma si hay cabeceras en respuestas correctas, flags de cookies, rutas autenticadas y diferencias HTTP/HTTPS. No informes vulnerabilidades de autorización solo porque existe un endpoint; reproduce el acceso indebido antes de afirmarlo.
8. Conserva reportes HTML/JSON sanitizados, capturas de alertas e información de cobertura en `auditoria/evidencias/zap/` y `auditoria/resultados/zap-antes.*`.

## Fase D — Una única mejora implementada y verificada

1. Selecciona **exactamente un hallazgo existente** de SonarQube o ZAP que pueda corregirse con un cambio mínimo y de bajo riesgo. Da preferencia a una corrección medible en entorno de laboratorio. Ejemplo condicional: si ZAP confirma falta de cabecera HTTP, evalúa añadir la cabecera apropiada en ASP.NET Core sin romper recursos ni autenticación. No presupongas que ese hallazgo existe.
2. Antes del cambio, registra ID de issue/alerta, evidencia, archivo, línea/ruta, valor inicial y prueba que demuestra el problema.
3. Implementa solo esa corrección. Si requiere varias líneas o pruebas, sigue considerándose una mejora cuando atiende al mismo hallazgo. Registra el diff concreto y por qué corrige la causa.
4. Ejecuta `dotnet build`, pruebas disponibles, smoke tests del navegador y funciones básicas (autenticación, roles, navegación, servicios e integraciones comprobables). No ejecutes pagos ni operaciones destructivas.
5. Repite **la herramienta que produjo el hallazgo**, misma configuración, perfil, versión de reglas y alcance cuando sea posible. Confirma que desapareció o cambió el hallazgo y evalúa si se produjeron regresiones; si no cambió, no declares éxito.
6. Compara antes/después en una tabla con: hallazgo, métrica inicial, cambio, métrica posterior, evidencia, resultado funcional. Cuando se comparen métricas agregadas, documenta su alcance y causas externas.
7. Conserva: evidencia de código anterior, diff, prueba previa, prueba posterior, dashboard/alerta posterior y resultados de funcionamiento.
8. No repares automáticamente otros hallazgos. Deja recomendaciones priorizadas para una etapa posterior.

## Fase E — Verificación técnica de dependencias distribuidas

Analiza de forma específica, con evidencia del proyecto y los scans:
- Qué componente origina cada hallazgo y qué clientes, dependencias o servicios pueden verse afectados.
- Cómo errores en autorización, cookies, sesiones, validaciones, tratamiento de errores, API o conexión a datos podrían propagarse.
- PostgreSQL: exposición del puerto publicado, controles de red y secretos, sin intentar obtener acceso no autorizado.
- Ollama: datos aportados al modelo, autorización previa al acceso a datos y tratamiento de las respuestas; no afirmar prompt injection sin evidencia.
- Integraciones de pago: manejo de callbacks, credenciales y validaciones; sin hacer pruebas destructivas.
- Background workers, SMTP, backups y restore: privilegios, trazabilidad y fallos en cadena.

Guarda hallazgos y limitaciones únicamente como datos técnicos estructurados: ID, fuente, severidad, archivo o endpoint, evidencia de la herramienta, estado de verificación y corrección propuesta. No generes texto para un informe.

## Archivos de ejecución y evidencias técnicas únicamente

Organiza los artefactos así:

```text
auditoria/
  README.md                      # comandos ejecutados y rutas de resultados
  resultados/
    00-linea-base.md
    inventario-componentes.json
    arquitectura.mmd
    endpoints.json
    hallazgos-tecnicos.csv
    sonarqube-antes.json
    zap-antes.json
    mejora-unica.diff
    comparacion-antes-despues.csv
    pruebas-funcionales.log
    limitaciones-tecnicas.txt
  evidencias/
    aplicacion/
    arquitectura/
    sonarqube/
    zap/
    mejora/
```

Puedes añadir `.json`, `.html`, logs sanitizados y capturas `.png` cuando sean obtenibles realmente. **No fabriques screenshots:** si no puedes capturarlos, anota exactamente cuál debe capturar el usuario en cada herramienta y en qué pantalla. No incluyas contraseñas, tokens, cookies, datos personales ni configuraciones sensibles en las imágenes. Conserva de forma segura los resultados originales antes de redactar resúmenes.

## Criterios de aceptación

- [ ] Orbi estaba funcionando antes de la auditoría y se verificó mediante evidencia.
- [ ] Arquitectura, tecnologías, puertos y endpoints se obtuvieron del sistema ejecutado, no solo de una descripción.
- [ ] Se ejecutó SonarScanner for .NET de forma efectiva; hay dashboard, métricas y estado verdadero del Quality Gate.
- [ ] Hay tres hallazgos SonarQube genuinos, con regla, archivo y explicación, o una limitación explícita si no existen.
- [ ] Se ejecutó ZAP contra el entorno autorizado; hay reportes y tres alertas genuinas, o una limitación explícita si no existen.
- [ ] Los hallazgos incluyen impacto sobre interacciones distribuidas y medidas de mitigación.
- [ ] Se aplicó una única mejora a un hallazgo inicial confirmado.
- [ ] Se repitió el análisis correspondiente y la comparación antes/después es trazable.
- [ ] Se comprobó que las funciones principales siguen operativas tras la corrección.
- [ ] Toda evidencia tiene ruta, fecha y contexto; ninguna es inventada.
- [ ] Solo se generaron resultados técnicos, capturas, comandos, logs y pruebas; no se redactó el informe ni se ejecutaron acciones destructivas.

## Forma de trabajo y respuesta esperada de Codex

Trabaja por fases de forma ordenada. Si tienes acceso operativo al entorno, **ejecuta** los comandos, verifica salidas y guarda evidencias en lugar de limitarte a describirlas. Si requieres un dato que solo el usuario tiene (token, ruta local del repositorio, cuenta de prueba, autorización para active scan), solicita únicamente ese dato en el momento indispensable, después de completar todo lo que no depende de él. Reporta fallos concretos y alternativas seguras.

Al terminar, entrega un resumen operativo breve: estado del entorno; SonarQube (métricas, Quality Gate, tres hallazgos); ZAP (tres alertas y cobertura); hallazgo corregido; resultado antes/después; verificación funcional; rutas exactas de evidencias; bloqueos pendientes. No redactes contenido académico: solamente estado de ejecución, comandos, métricas, alertas, archivos y comprobaciones.

## Referencias de ejecución

- SonarScanner for .NET (SonarQube Community Build): https://docs.sonarsource.com/sonarqube-community-build/analyzing-source-code/scanners/dotnet/using
- OWASP ZAP Baseline Scan: https://www.zaproxy.org/docs/docker/baseline-scan/
- OWASP ZAP Full Scan (solo laboratorio autorizado): https://www.zaproxy.org/docs/docker/full-scan/