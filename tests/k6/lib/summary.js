import { RUN_ID, SUMMARY_PATH } from './config.js';

export function handleSummary(data) {
  return {
    [SUMMARY_PATH]: JSON.stringify(data, null, 2),
    stdout: `k6 run ${RUN_ID}: summary written to ${SUMMARY_PATH}\n`,
  };
}
