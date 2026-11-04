import { sleep } from 'k6';
import { readOnlyGet } from '../lib/requests.js';
import { profileOptions } from '../lib/profiles.js';
import { handleSummary } from '../lib/summary.js';

export const options = profileOptions('rollout', { vus: 20, duration: '90s' });

export default function () {
  readOnlyGet();
  sleep(1);
}

export { handleSummary };
