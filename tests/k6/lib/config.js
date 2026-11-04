import { fail } from 'k6';

const allowedPaths = new Set([
  '/health/live',
  '/api/identity/registration/options',
]);

const baseUrl = (__ENV.ORBI_K6_BASE_URL || 'http://host.docker.internal:30083').replace(/\/$/, '');
const targetPath = __ENV.ORBI_K6_TARGET_PATH || '/api/identity/registration/options';
const runId = __ENV.ORBI_K6_RUN_ID || 'static-validation';
const timeout = __ENV.ORBI_K6_TIMEOUT || '10s';

function validate() {
  if (!/^http:\/\/host\.docker\.internal:30083$/.test(baseUrl)) {
    fail('BASE_URL debe ser una URL válida del laboratorio');
  }
  if (!allowedPaths.has(targetPath)) {
    fail(`ORBI_K6_TARGET_PATH no está en la lista de GET de solo lectura: ${targetPath}`);
  }
  if (!/^[A-Za-z0-9][A-Za-z0-9_-]{0,79}$/.test(runId)) {
    fail('ORBI_K6_RUN_ID requerido: use letras, números, guiones o guiones bajos (máximo 80 caracteres)');
  }
  if (!/^([1-9][0-9]?|1[0-9]{2}|200|[1-9][0-9]{2}|500)(ms|s|m)$/.test(timeout)) {
    fail('ORBI_K6_TIMEOUT debe ser una duración positiva en ms, s o m');
  }
}

validate();

export const BASE_URL = baseUrl;
export const TARGET_PATH = targetPath;
export const REQUEST_TIMEOUT = timeout;
export const RUN_ID = runId;
export const SUMMARY_PATH = `/results/${runId}/k6-summary.json`;
export const PROMETHEUS_TAGS = { run_id: runId };

export function scenarioOptions(profile, defaults) {
  const vus = Number(__ENV.ORBI_K6_VUS_TARGET || defaults.vus);
  const duration = __ENV.ORBI_K6_DURATION || defaults.duration;

  if (!Number.isInteger(vus) || vus < 1 || vus > 500) {
    fail('ORBI_K6_VUS_TARGET debe ser un entero entre 1 y 500');
  }
  if (vus > 100 && __ENV.ORBI_K6_ALLOW_STRESS !== 'true') {
    fail('Más de 100 VUs requiere ORBI_K6_ALLOW_STRESS=true y autorización explícita de laboratorio');
  }
  const durationSeconds = Number(duration.replace(/s$/, ''));
  if (!/^([1-9][0-9]{0,2}|500)s$/.test(duration) || durationSeconds > 500) {
    fail('ORBI_K6_DURATION debe ser una duración entera entre 1s y 500s');
  }

  return {
    scenarios: {
      [profile]: {
        executor: 'constant-vus',
        vus,
        duration,
        gracefulStop: '5s',
        tags: { profile },
      },
    },
    discardResponseBodies: true,
    noConnectionReuse: false,
    thresholds: {
      http_req_failed: ['rate<0.01'],
      checks: ['rate>0.99'],
      business_failures: ['rate<0.01'],
      http_req_duration: ['p(95)<1000'],
    },
    tags: { run_id: runId },
  };
}
