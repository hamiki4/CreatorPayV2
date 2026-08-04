import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  scenarios: {
    steady_readiness: { executor: 'constant-vus', vus: 20, duration: '2m' },
  },
  thresholds: {
    http_req_failed: ['rate<0.001'],
    http_req_duration: ['p(95)<250', 'p(99)<500'],
    checks: ['rate>0.999'],
  },
};

const baseUrl = (__ENV.BASE_URL || '').replace(/\/$/, '');
if (!baseUrl.startsWith('https://')) throw new Error('BASE_URL must be an HTTPS UAT endpoint');

export default function () {
  const response = http.get(`${baseUrl}/health/ready`, { tags: { operation: 'readiness' } });
  check(response, {
    'readiness is 200': (r) => r.status === 200,
    'readiness is healthy': (r) => r.json('status') === 'Healthy',
  });
  sleep(1);
}
