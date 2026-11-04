import http from 'k6/http';
import { check } from 'k6';
import { Rate } from 'k6/metrics';
import { BASE_URL, PROMETHEUS_TAGS, REQUEST_TIMEOUT, TARGET_PATH } from './config.js';

export const businessFailures = new Rate('business_failures');

export function readOnlyGet() {
  const response = http.get(`${BASE_URL}${TARGET_PATH}`, {
    timeout: REQUEST_TIMEOUT,
    tags: { endpoint: TARGET_PATH, ...PROMETHEUS_TAGS },
  });
  const isOk = response.status === 200;
  businessFailures.add(isOk ? 0 : 1, { endpoint: TARGET_PATH });
  check(response, {
    [`GET ${TARGET_PATH} returns HTTP 200`]: (res) => res.status === 200,
  });
  return response;
}

export function liveGet() {
  const response = http.get(`${BASE_URL}/health/live`, {
    timeout: REQUEST_TIMEOUT,
    tags: { endpoint: '/health/live', ...PROMETHEUS_TAGS },
  });
  const isOk = response.status === 200;
  businessFailures.add(isOk ? 0 : 1, { endpoint: '/health/live' });
  check(response, {
    'GET /health/live returns HTTP 200': (res) => res.status === 200,
  });
  return response;
}
