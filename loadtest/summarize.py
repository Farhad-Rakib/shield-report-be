#!/usr/bin/env python3
"""Summarise docker-stats CSV per phase and container: avg/max CPU %, avg/max memory MiB."""
import csv, sys
from collections import defaultdict

rows = list(csv.DictReader(open(sys.argv[1])))
agg = defaultdict(lambda: {"cpu": [], "mem": []})
totals = defaultdict(lambda: defaultdict(lambda: [0.0, 0.0]))  # phase -> epoch -> [cpu, mem]
for r in rows:
    key = (r["phase"], r["container"])
    cpu, mem = float(r["cpu_percent"] or 0), float(r["mem_mib"] or 0)
    agg[key]["cpu"].append(cpu); agg[key]["mem"].append(mem)
    t = totals[r["phase"]][r["epoch"]]; t[0] += cpu; t[1] += mem

print("| Phase | Container | CPU avg % | CPU max % | Mem avg MiB | Mem max MiB | Samples |")
print("|---|---|---:|---:|---:|---:|---:|")
for (phase, c), v in sorted(agg.items()):
    n = len(v["cpu"])
    print(f"| {phase} | {c} | {sum(v['cpu'])/n:.1f} | {max(v['cpu']):.1f} | {sum(v['mem'])/n:.0f} | {max(v['mem']):.0f} | {n} |")
print()
print("| Phase | Stack CPU avg % | Stack CPU peak % | Stack mem avg MiB | Stack mem peak MiB |")
print("|---|---:|---:|---:|---:|")
for phase, by_epoch in totals.items():
    cpus = [x[0] for x in by_epoch.values()]; mems = [x[1] for x in by_epoch.values()]
    print(f"| {phase} | {sum(cpus)/len(cpus):.1f} | {max(cpus):.1f} | {sum(mems)/len(mems):.0f} | {max(mems):.0f} |")
