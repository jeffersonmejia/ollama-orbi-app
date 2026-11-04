allow_k8s_contexts('docker-desktop')
runner_dll = os.path.abspath('tools/OrbiApp.LoadTestRunner/bin/Release/net10.0/tilt/OrbiApp.LoadTestRunner.dll')
repo_root = os.path.abspath('.')

docker_compose([
    'docker-compose.yml',
    'docker-compose.monitoring.yml',
    'docker-compose.k6.yml',
], project_name='orbi-app')

docker_build('orbiapp:lab', '.', dockerfile='Dockerfile', ignore=['tools', 'tests', 'infra', 'docs', 'Tiltfile', 'docker-compose*.yml', '.docker'])
k8s_yaml(kustomize('infra/kubernetes/base'))
k8s_resource(
    'orbi',
    objects=[
        'orbi-lab-config:configmap:orbi-lab',
        'orbi-lab-data-protection:persistentvolumeclaim:orbi-lab',
        'orbi-lab-backups:persistentvolumeclaim:orbi-lab',
    ],
    port_forwards=['0.0.0.0:30083:8080'],
    resource_deps=['orbi-lab-preflight', 'orbi-lab-secret', 'postgres', 'prometheus', 'grafana'],
)

# Build the runner once; Tilt's local resources invoke its DLL to avoid
# concurrent `dotnet run` builds while the event server is serving requests.
local_resource(
    'orbi-runner-build',
    cmd=['dotnet', 'publish', 'tools/OrbiApp.LoadTestRunner', '--configuration', 'Release', '--no-restore', '--output', 'tools/OrbiApp.LoadTestRunner/bin/Release/net10.0/tilt'],
    deps=[
        'tools/OrbiApp.LoadTestRunner/CommandRunner.cs',
        'tools/OrbiApp.LoadTestRunner/EnvironmentValidator.cs',
        'tools/OrbiApp.LoadTestRunner/EventPublisher.cs',
        'tools/OrbiApp.LoadTestRunner/EventsServer.cs',
        'tools/OrbiApp.LoadTestRunner/K6Executor.cs',
        'tools/OrbiApp.LoadTestRunner/KubernetesController.cs',
        'tools/OrbiApp.LoadTestRunner/LabPaths.cs',
        'tools/OrbiApp.LoadTestRunner/LabSecretManager.cs',
        'tools/OrbiApp.LoadTestRunner/Options.cs',
        'tools/OrbiApp.LoadTestRunner/Program.cs',
        'tools/OrbiApp.LoadTestRunner/PrometheusClient.cs',
        'tools/OrbiApp.LoadTestRunner/ReportWriter.cs',
        'tools/OrbiApp.LoadTestRunner/RunModels.cs',
        'tools/OrbiApp.LoadTestRunner/OrbiApp.LoadTestRunner.csproj',
    ],
    dir=repo_root,
    auto_init=True,
)

local_resource(
    'orbi-lab-preflight',
    cmd=['dotnet', runner_dll, 'preflight'],
    deps=['infra/kubernetes/base'],
    resource_deps=['orbi-runner-build'],
    dir=repo_root,
    auto_init=True,
)

# Create/update the lab credential from the local secret file without printing it.
local_resource(
    'orbi-lab-secret',
    cmd=['dotnet', runner_dll, 'lab-secret'],
    deps=['.docker/secrets/orbi_app_password'],
    resource_deps=['orbi-runner-build', 'orbi-lab-preflight', 'postgres'],
    dir=repo_root,
    auto_init=True,
)

local_resource(
    'orbi-events',
    serve_cmd=['dotnet', runner_dll, 'events'],
    resource_deps=['orbi-runner-build'],
    auto_init=True,
    serve_dir=repo_root,
    links=['http://127.0.0.1:15190'],
    readiness_probe=probe(period_secs=2, http_get=http_get_action(port=15190, path='/health')),
)

# Tests are manual resources. Tilt up starts no load.
local_resource('orbi-smoke', cmd=['dotnet', runner_dll, 'smoke'],
    deps=['tests/k6/lib', 'tests/k6/scenarios'], resource_deps=['orbi-runner-build', 'orbi', 'orbi-events'],
    trigger_mode=TRIGGER_MODE_MANUAL, auto_init=False, dir=repo_root)
local_resource('orbi-baseline', cmd=['dotnet', runner_dll, 'baseline'],
    deps=['tests/k6/lib', 'tests/k6/scenarios'], resource_deps=['orbi-runner-build', 'orbi', 'orbi-events'],
    trigger_mode=TRIGGER_MODE_MANUAL, auto_init=False, dir=repo_root)
local_resource('orbi-matrix-100', cmd=['dotnet', runner_dll, 'matrix', '--max-vus', '100', '--confirm-multi-replica'],
    deps=['tests/k6/lib', 'tests/k6/scenarios'], resource_deps=['orbi-runner-build', 'orbi', 'orbi-events'],
    trigger_mode=TRIGGER_MODE_MANUAL, auto_init=False, dir=repo_root)
local_resource('orbi-availability', cmd=['dotnet', runner_dll, 'availability', '--confirm-pod-delete', '--lab-only'],
    deps=['tests/k6/lib', 'tests/k6/scenarios'], resource_deps=['orbi-runner-build', 'orbi', 'orbi-events'],
    trigger_mode=TRIGGER_MODE_MANUAL, auto_init=False, dir=repo_root)
local_resource('orbi-report', cmd=['dotnet', runner_dll, 'report'],
    resource_deps=['orbi-runner-build'], trigger_mode=TRIGGER_MODE_MANUAL, auto_init=False, dir=repo_root)
