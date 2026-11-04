import { sleep } from 'k6';
import { liveGet } from '../lib/requests.js';
import { profileOptions } from '../lib/profiles.js';
import { handleSummary } from '../lib/summary.js';

export const options = profileOptions('smoke', { vus: 1, duration: '10s' });

export default function () {
  liveGet();
  sleep(1);
}

export { handleSummary };
