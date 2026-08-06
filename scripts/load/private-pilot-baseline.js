import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  scenarios: { private_pilot: { executor: 'constant-vus', vus: 5, duration: '3m' } },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<750', 'p(99)<1500'],
    'http_req_duration{operation:health}': ['p(95)<250'],
    'http_req_duration{operation:signin}': ['p(95)<1000'],
  },
};

const api = (__ENV.API_URL || '').replace(/\/$/, '');
const web = (__ENV.WEB_URL || '').replace(/\/$/, '');
if (!api.startsWith('https://') || !web.startsWith('https://')) throw new Error('API_URL and WEB_URL must use HTTPS');

function get(path, token, operation) {
  return http.get(`${api}${path}`, { headers: token ? { Authorization: `Bearer ${token}` } : {}, tags: { operation } });
}

export function setup() {
  const identities = [
    ['CREATOR_EMAIL', 'CREATOR_PASSWORD', 'creatorToken'],
    ['ADMIN_EMAIL', 'ADMIN_PASSWORD', 'adminToken'],
    ['CASHIER_EMAIL', 'CASHIER_PASSWORD', 'cashierToken'],
  ];
  const tokens = {};
  for (const [emailKey, passwordKey, tokenKey] of identities) {
    if (!__ENV[emailKey] || !__ENV[passwordKey]) continue;
    const response = http.post(`${api}/api/v1/auth/login`, JSON.stringify({ email: __ENV[emailKey], password: __ENV[passwordKey] }), { headers: { 'Content-Type': 'application/json' }, tags: { operation: 'signin' } });
    check(response, { [`${tokenKey} sign-in succeeds`]: (r) => r.status === 200 });
    tokens[tokenKey] = response.json('accessToken');
  }
  return tokens;
}

export default function (tokens) {
  check(get('/health/live', null, 'health'), { 'liveness 200': (r) => r.status === 200 });
  check(get('/health/ready', null, 'health'), { 'readiness 200': (r) => r.status === 200 });
  check(http.get(`${web}/help`, { tags: { operation: 'help' } }), { 'Help page 200': (r) => r.status === 200 });
  if (tokens.creatorToken) check(get('/api/v1/creator/campaigns', tokens.creatorToken, 'creator_offers'), { 'Creator Offers 200': (r) => r.status === 200 });
  if (tokens.creatorToken && __ENV.CAMPAIGN_ID) check(get(`/api/v1/creator/campaigns/${__ENV.CAMPAIGN_ID}/qr-image`, tokens.creatorToken, 'qr'), { 'QR retrieval 200': (r) => r.status === 200 });
  if (tokens.adminToken) check(get('/api/v1/admin/dashboard/pilot-operations', tokens.adminToken, 'admin_pilot'), { 'Pilot Operations 200': (r) => r.status === 200 });

  // Mutating probes require an explicit opt-in and isolated synthetic records.
  if (__ITER === 0 && __VU === 1 && __ENV.ENABLE_WRITE_PROBES === 'true' && __ENV.SUPPORT_CONTACT) {
    const body = { name: 'Synthetic Pilot Probe', contact: __ENV.SUPPORT_CONTACT, userType: 'Other', subject: 'Synthetic performance probe', message: 'Synthetic private-pilot performance validation record.', preferredLanguage: 'en', consentAcknowledged: true };
    check(http.post(`${api}/api/v1/support/requests`, JSON.stringify(body), { headers: { 'Content-Type': 'application/json' }, tags: { operation: 'support' } }), { 'support accepted': (r) => r.status === 200 });
  }
  if (__ITER === 0 && __VU === 1 && __ENV.ENABLE_FINANCIAL_PROBES === 'true' && tokens.cashierToken && __ENV.CHECKOUT_BODY) {
    check(http.post(`${api}/api/v1/cashier/checkouts/offer`, __ENV.CHECKOUT_BODY, { headers: { Authorization: `Bearer ${tokens.cashierToken}`, 'Content-Type': 'application/json', 'Idempotency-Key': `perf-${__VU}-${__ITER}` }, tags: { operation: 'cashier_checkout' } }), { 'checkout handled': (r) => [200, 409, 422].includes(r.status) });
  }
  sleep(1);
}
