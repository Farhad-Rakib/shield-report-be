#!/usr/bin/env bash
# Full measurement run: idle baseline, E2E suite, API ramp, spike, report rendering.
# Raises the per-user rate limit for the duration (single test user), restores it at the end.
set -u
cd "$(dirname "$0")"
OUT=results; mkdir -p "$OUT"; CSV="$OUT/docker-stats.csv"; rm -f "$CSV"
BE=..; FE=../../shield-report-fe
API=http://127.0.0.1:5050/api/v1
wait_healthy() { for i in $(seq 1 60); do [ "$(docker inspect -f '{{.State.Health.Status}}' shieldreport-api)" = healthy ] && return 0; sleep 3; done; return 1; }
phase() { # phase <label> <command...>
  local label=$1; shift
  ./sample-docker-stats.sh "$CSV" "$label" & local sampler=$!
  local start=$(date +%s)
  "$@"; local rc=$?
  echo "$label,$(( $(date +%s) - start )),$rc" >> "$OUT/phases.csv"
  kill $sampler 2>/dev/null; wait $sampler 2>/dev/null
}
echo "phase,seconds,exit_code" > "$OUT/phases.csv"

echo "== raising rate limit for the run"
(cd $BE && RATE_LIMIT_PER_USER_PER_MINUTE=1000000 RATE_LIMIT_ANONYMOUS_PER_MINUTE=1000 docker compose up -d api >/dev/null 2>&1); wait_healthy

phase idle sleep 60
phase e2e bash -c "cd $FE && npx playwright test > ../shield-report-be/loadtest/$OUT/e2e-output.txt 2>&1"
cp "$FE/e2e-results/results.json" "$OUT/e2e-results.json" 2>/dev/null
phase api-ramp k6 run -q -e BASE_URL=$API -e SCENARIO=api --summary-export "$OUT/k6-api.json" api-load.js > "$OUT/k6-api.txt" 2>&1
phase cooldown sleep 20
phase spike k6 run -q -e BASE_URL=$API -e SCENARIO=spike --summary-export "$OUT/k6-spike.json" api-load.js > "$OUT/k6-spike.txt" 2>&1
phase cooldown2 sleep 20
phase render k6 run -q -e BASE_URL=$API -e SCENARIO=render -e ENGAGEMENT_ID=${ENGAGEMENT_ID:-3} --summary-export "$OUT/k6-render.json" api-load.js > "$OUT/k6-render.txt" 2>&1
phase after sleep 30

echo "== restoring rate limit"
(cd $BE && docker compose up -d api >/dev/null 2>&1); wait_healthy
python3 summarize.py "$CSV" > "$OUT/resource-summary.md"
echo done
