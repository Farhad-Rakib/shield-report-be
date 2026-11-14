#!/usr/bin/env bash
# Samples `docker stats` for the ShieldReport containers every ~2s into a CSV until killed.
#   ./sample-docker-stats.sh out.csv phase-label
# Columns: epoch,phase,container,cpu_percent,mem_mib
set -u
OUT="$1"; PHASE="${2:-run}"
[ -f "$OUT" ] || echo "epoch,phase,container,cpu_percent,mem_mib" > "$OUT"
to_mib() { # "1.2GiB" / "512MiB" / "800KiB" -> MiB
  awk -v v="$1" 'BEGIN{ n=v+0; if (v ~ /GiB/) n*=1024; else if (v ~ /KiB/) n/=1024; else if (v ~ /B$/ && v !~ /iB/) n/=1048576; printf "%.1f", n }'
}
while true; do
  now=$(date +%s)
  docker stats --no-stream --format '{{.Name}}|{{.CPUPerc}}|{{.MemUsage}}' 2>/dev/null | while IFS='|' read -r name cpu mem; do
    case "$name" in shieldreport-*|*shield*) ;; *) continue ;; esac
    used="${mem%% /*}"
    echo "$now,$PHASE,$name,${cpu%\%},$(to_mib "$used")" >> "$OUT"
  done
  sleep 1
done
