import { fail } from 'k6';
import { scenarioOptions } from './config.js';

export function profileOptions(profile, defaults) {
  if (__ENV.ORBI_K6_PROFILE && __ENV.ORBI_K6_PROFILE !== profile) {
    fail(`El script ${profile} no acepta ORBI_K6_PROFILE=${__ENV.ORBI_K6_PROFILE}`);
  }
  return scenarioOptions(profile, defaults);
}
