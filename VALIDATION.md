# GRIP BREAK 0.4.13 Windows validation

Native f195: zero Unity errors/warnings. Current checks: 49/49 passed, zero issues. Categories: {"render-optimization": 13, "camera-obstruction": 25, "hand-contact": 11}. Current checks cover exact shared loop geometry/materials/transforms/shadows/physics configuration, camera obstruction, and held-hand visuals. Root inspected 14 current captures.

Two current actual High1080p full-scene offscreen runs measured 114.33 and 115.78 FPS; final p50=8.3415ms,p95=9.2346ms. Saved same-render reference 73.06 FPS was reused without rerunning an old build. Same workload,92 registered bodies,240Hz,64/24solver,4xMSAA,full geometry/materials/lights and unchanged audit logging. This meets the 90 FPS target on this Windows machine and workload; it is not an all-system,physical-Mac,online or grappling guarantee.

Production changes: GPU batched skinning and sharing identical torus mesh data by exact(radius,tube). Automatic GPU instancing regressed and was removed. No static batching or manual mesh merging ships. Historical0.4.12 physics/gameplay and 0.4.11 session/shutdown/updater reports are source-verified unchanged-code evidence, not new f195 reruns.

See release-evidence/0.4.13 for current and historical raw proof. Separate-machine internet acceptance,long human matches and physical-Mac performance remain unverified. Public bytes are verified after publication.
