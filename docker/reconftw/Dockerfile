# Lean reconftw image for ShieldReport's "Reconnaissance" step.
# Upstream image (pinned by digest so the config below always matches the reconftw.cfg it
# overrides) plus reconftw.lean.cfg, which DockerScanRunner passes via `-f`.
# Upstream only publishes linux/arm64; on amd64 hosts this runs under QEMU emulation.
FROM six2dez/reconftw@sha256:b250cea53c5531de408ef4da753e7a30ed0f46cb4b32569e41065ef4c729626f
COPY reconftw.lean.cfg /reconftw/reconftw.lean.cfg
