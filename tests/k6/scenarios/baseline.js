import { sleep } from 'k6';
import { readOnlyGet } from '../lib/requests.js';
import { profileOptions } from '../lib/profiles.js';
import { handleSummary } from '../lib/summary.js';

export const options = profileOptions('baseline', { vus: 100, duration: '30s' });

export default function () {
  readOnlyGet();
  sleep(1);
}

export { handleSummary };
