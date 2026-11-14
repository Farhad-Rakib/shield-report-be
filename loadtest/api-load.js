// k6 load test for the ShieldReport API (read-heavy staff traffic + report rendering).
//   k6 run -e BASE_URL=http://127.0.0.1:5050/api/v1 -e SCENARIO=api api-load.js
//   k6 run -e BASE_URL=... -e SCENARIO=render -e ENGAGEMENT_ID=3 api-load.js
// Credentials: EMAIL / PASSWORD env vars (default: the seeded local super admin).
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';

const BASE = __ENV.BASE_URL || 'http://127.0.0.1:5050/api/v1';
const SCENARIO = __ENV.SCENARIO || 'api';
const ENGAGEMENT_ID = __ENV.ENGAGEMENT_ID || '3';

const endpointTrend = {
  dashboard: new Trend('ep_dashboard', true),
  engagements: new Trend('ep_engagements', true),
  vulnerabilities: new Trend('ep_vulnerabilities', true),
  scans: new Trend('ep_scans', true),
  menu: new Trend('ep_menu', true),
  reports: new Trend('ep_reports', true),
  preview: new Trend('ep_report_preview', true),
};

const scenarios = {
  // Ramp to 50 concurrent users clicking around list pages (~1 request/s each).
  api: {
    executor: 'ramping-vus',
    startVUs: 0,
    stages: [
      { duration: '30s', target: 10 },
      { duration: '60s', target: 25 },
      { duration: '60s', target: 50 },
      { duration: '60s', target: 50 },
      { duration: '20s', target: 0 },
    ],
    exec: 'browse',
  },
  // Spike: 100 users with no think time, to find the ceiling.
  spike: {
    executor: 'constant-vus',
    vus: 100,
    duration: '60s',
    exec: 'hammer',
  },
  // Report rendering (headless Chromium + Handlebars): 4 users previewing continuously.
  render: {
    executor: 'constant-vus',
    vus: 4,
    duration: '90s',
    exec: 'render',
  },
};

export const options = {
  scenarios: { [SCENARIO]: scenarios[SCENARIO] },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: SCENARIO === 'render' ? ['p(95)<15000'] : ['p(95)<1000'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const res = http.post(`${BASE}/Auth/login`, JSON.stringify({
    email: __ENV.EMAIL || 'superadmin@localhost',
    password: __ENV.PASSWORD || 'SuperAdmin@123!',
  }), { headers: { 'Content-Type': 'application/json' } });
  check(res, { 'login ok': (r) => r.status === 200 });
  return { token: res.json('data.accessToken') };
}

function get(token, path, trend) {
  const res = http.get(`${BASE}${path}`, { headers: { Authorization: `Bearer ${token}` }, tags: { name: path.split('?')[0] } });
  check(res, { [`${path.split('?')[0]} 200`]: (r) => r.status === 200 });
  trend.add(res.timings.duration);
  return res;
}

export function browse(data) {
  const t = data.token;
  get(t, '/menu', endpointTrend.menu);
  get(t, '/dashboard', endpointTrend.dashboard);
  sleep(1);
  get(t, '/engagements?page=1&pageSize=20', endpointTrend.engagements);
  sleep(1);
  get(t, '/vulnerabilities?page=1&pageSize=20', endpointTrend.vulnerabilities);
  sleep(1);
  get(t, '/scans?page=1&pageSize=20', endpointTrend.scans);
  sleep(1);
  get(t, `/engagements/${ENGAGEMENT_ID}/reports`, endpointTrend.reports);
  sleep(1);
}

export function hammer(data) {
  const t = data.token;
  get(t, '/dashboard', endpointTrend.dashboard);
  get(t, '/engagements?page=1&pageSize=20', endpointTrend.engagements);
  get(t, '/vulnerabilities?page=1&pageSize=20', endpointTrend.vulnerabilities);
  get(t, '/scans?page=1&pageSize=20', endpointTrend.scans);
}

export function render(data) {
  const res = http.post(`${BASE}/engagements/${ENGAGEMENT_ID}/reports/preview`, JSON.stringify({ templateId: null, testingType: 'Gray', showToolNames: false }), {
    headers: { Authorization: `Bearer ${data.token}`, 'Content-Type': 'application/json' },
    timeout: '60s',
    tags: { name: 'report-preview' },
  });
  check(res, { 'preview 200': (r) => r.status === 200 });
  endpointTrend.preview.add(res.timings.duration);
}
