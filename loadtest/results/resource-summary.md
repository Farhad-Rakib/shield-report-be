| Phase | Container | CPU avg % | CPU max % | Mem avg MiB | Mem max MiB | Samples |
|---|---|---:|---:|---:|---:|---:|
| after | shieldreport-api | 3.9 | 5.1 | 702 | 706 | 10 |
| after | shieldreport-backup | 0.0 | 0.0 | 8 | 8 | 10 |
| after | shieldreport-fe | 0.0 | 0.0 | 10 | 10 | 10 |
| after | shieldreport-mailhog | 0.0 | 0.0 | 19 | 19 | 10 |
| after | shieldreport-mssql | 4.2 | 15.8 | 1492 | 1492 | 10 |
| after | shieldreport-proxy | 0.0 | 0.0 | 79 | 79 | 10 |
| api-ramp | shieldreport-api | 23.3 | 42.1 | 722 | 901 | 76 |
| api-ramp | shieldreport-backup | 0.0 | 0.0 | 8 | 8 | 76 |
| api-ramp | shieldreport-fe | 0.0 | 0.0 | 10 | 10 | 76 |
| api-ramp | shieldreport-mailhog | 0.0 | 0.1 | 18 | 19 | 76 |
| api-ramp | shieldreport-mssql | 15.1 | 30.0 | 1450 | 1454 | 76 |
| api-ramp | shieldreport-proxy | 0.0 | 0.7 | 134 | 145 | 76 |
| cooldown | shieldreport-api | 29.5 | 189.7 | 628 | 643 | 7 |
| cooldown | shieldreport-backup | 0.0 | 0.0 | 8 | 8 | 7 |
| cooldown | shieldreport-fe | 0.0 | 0.0 | 10 | 10 | 7 |
| cooldown | shieldreport-mailhog | 0.0 | 0.0 | 19 | 19 | 7 |
| cooldown | shieldreport-mssql | 21.6 | 137.2 | 1453 | 1455 | 7 |
| cooldown | shieldreport-proxy | 0.0 | 0.0 | 86 | 123 | 7 |
| cooldown2 | shieldreport-api | 3.0 | 3.9 | 685 | 685 | 7 |
| cooldown2 | shieldreport-backup | 0.0 | 0.0 | 8 | 8 | 7 |
| cooldown2 | shieldreport-fe | 0.0 | 0.0 | 10 | 10 | 7 |
| cooldown2 | shieldreport-mailhog | 0.0 | 0.0 | 19 | 19 | 7 |
| cooldown2 | shieldreport-mssql | 6.7 | 14.9 | 1489 | 1497 | 7 |
| cooldown2 | shieldreport-proxy | 0.0 | 0.0 | 79 | 79 | 7 |
| e2e | shieldreport-api | 46.3 | 192.7 | 473 | 710 | 17 |
| e2e | shieldreport-backup | 0.0 | 0.0 | 8 | 8 | 17 |
| e2e | shieldreport-fe | 0.1 | 0.5 | 11 | 11 | 17 |
| e2e | shieldreport-mailhog | 0.0 | 0.0 | 17 | 17 | 17 |
| e2e | shieldreport-mssql | 6.1 | 21.2 | 1444 | 1445 | 17 |
| e2e | shieldreport-proxy | 2.1 | 10.6 | 141 | 146 | 17 |
| e2e | shieldreport-scan-60034 | 0.4 | 0.5 | 1 | 1 | 8 |
| idle | shieldreport-api | 5.6 | 53.5 | 164 | 189 | 20 |
| idle | shieldreport-backup | 0.0 | 0.0 | 8 | 8 | 20 |
| idle | shieldreport-fe | 0.0 | 0.3 | 10 | 10 | 20 |
| idle | shieldreport-mailhog | 0.0 | 0.0 | 17 | 17 | 20 |
| idle | shieldreport-mssql | 3.1 | 14.6 | 1444 | 1444 | 20 |
| idle | shieldreport-proxy | 0.2 | 3.1 | 133 | 133 | 20 |
| render | shieldreport-api | 239.3 | 277.1 | 724 | 818 | 30 |
| render | shieldreport-backup | 0.0 | 0.0 | 8 | 8 | 30 |
| render | shieldreport-fe | 0.0 | 0.0 | 10 | 10 | 30 |
| render | shieldreport-mailhog | 0.0 | 0.1 | 19 | 19 | 30 |
| render | shieldreport-mssql | 9.7 | 21.1 | 1489 | 1492 | 30 |
| render | shieldreport-proxy | 0.0 | 0.6 | 79 | 79 | 30 |
| spike | shieldreport-api | 425.3 | 452.4 | 747 | 945 | 20 |
| spike | shieldreport-backup | 0.0 | 0.0 | 8 | 8 | 20 |
| spike | shieldreport-fe | 0.0 | 0.0 | 10 | 10 | 20 |
| spike | shieldreport-mailhog | 0.0 | 0.0 | 19 | 19 | 20 |
| spike | shieldreport-mssql | 331.5 | 336.2 | 1485 | 1494 | 20 |
| spike | shieldreport-proxy | 0.0 | 0.0 | 79 | 79 | 20 |

| Phase | Stack CPU avg % | Stack CPU peak % | Stack mem avg MiB | Stack mem peak MiB |
|---|---:|---:|---:|---:|
| idle | 8.9 | 66.4 | 1777 | 1802 |
| e2e | 54.7 | 198.8 | 2093 | 2336 |
| api-ramp | 38.5 | 65.2 | 2342 | 2528 |
| cooldown | 51.0 | 326.9 | 2205 | 2239 |
| spike | 756.8 | 773.1 | 2349 | 2539 |
| cooldown2 | 9.7 | 17.7 | 2291 | 2299 |
| render | 249.0 | 286.0 | 2330 | 2423 |
| after | 8.1 | 19.7 | 2311 | 2314 |
