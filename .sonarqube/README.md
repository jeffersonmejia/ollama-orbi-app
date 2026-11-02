# SonarQube y observabilidad local de Dash

Este stack conserva la instancia y los datos de SonarQube que ya usaba la auditoría. Los puertos publicados se limitan a `127.0.0.1`.

Los contenedores tienen límites de recursos moderados: SonarQube 3.5 CPU y 5 GiB, Prometheus 0.75 CPU y 768 MiB, Grafana 0.75 CPU y 512 MiB, y Telegraf 0.5 CPU y 384 MiB. En conjunto reservan un máximo de 5.5 CPU y 6.6 GiB para este stack.

## Inicio

El archivo `.sonarqube/.env` ya contiene credenciales aleatorias locales y está excluido de Git. Si se crea el entorno en otra máquina, copiar `.env.example` a `.env`, generar una contraseña aleatoria para Grafana y establecer `GRAFANA_BASIC_AUTH` con el valor Base64 de `admin:<contraseña>`.

Desde la raíz del repositorio:

```powershell
docker compose --env-file .sonarqube/.env -f .sonarqube/docker-compose.yml up -d
docker compose --env-file .sonarqube/.env -f .sonarqube/docker-compose.yml ps
```

- SonarQube: <http://localhost:9001>
- Prometheus: <http://localhost:9090>
- Grafana: <http://localhost:3000> (usuario `admin`; contraseña local en `.sonarqube/.env`)

En el primer inicio de una base SonarQube vacía, entrar con `admin/admin` y cambiar la contraseña cuando la interfaz lo solicite.

## Análisis .NET

Instala el scanner una vez si aún no está instalado:

```powershell
dotnet tool install --global dotnet-sonarscanner
```

En PowerShell, crea un token de análisis en SonarQube y guárdalo en la variable de entorno `SONAR_TOKEN`; no lo escribas en archivos versionados. Después ejecuta:

```powershell
dotnet sonarscanner begin /k:"dash-app" /n:"Dash" /s:"$PWD\.sonarqube\SonarQube.Analysis.xml" /d:sonar.token="$env:SONAR_TOKEN"
dotnet build OrbiApp.slnx --configuration Release
dotnet sonarscanner end /d:sonar.token="$env:SONAR_TOKEN"
```

La configuración del scanner fija el servidor local y excluye artefactos generados, bibliotecas web y resultados de auditoría. El token se entrega solo al proceso del scanner.

## Métricas y actualización en vivo

El perfil `http` de `Presentation` exporta métricas OTLP/HTTP cada 5 segundos a Prometheus por `localhost:9090`. Esto no requiere publicar un puerto adicional del backend. La instrumentación cubre duración/volumen de solicitudes ASP.NET Core, solicitudes HTTP salientes y métricas del runtime .NET (GC, heap, excepciones y ThreadPool).

Prometheus conserva 14 días y Grafana consulta su datasource cada 5 segundos. Además, Telegraf consulta PromQL cada segundo y entrega solicitudes por segundo, p95, hilos y cola del ThreadPool a Grafana Live por WebSocket. El dashboard provisionado muestra series históricas de Prometheus y paneles Live; el stream de Grafana es efímero y está pensado para ver el estado inmediato.

El usuario/contraseña de Grafana se usa internamente para autenticar el WebSocket de Telegraf. No se imprimen credenciales en la documentación y el archivo `.env` permanece local.

## Parada

```powershell
docker compose --env-file .sonarqube/.env -f .sonarqube/docker-compose.yml down
```

Los volúmenes conservan análisis y métricas. Para borrar datos manualmente, hacerlo únicamente con una decisión explícita; `down` no elimina volúmenes.
