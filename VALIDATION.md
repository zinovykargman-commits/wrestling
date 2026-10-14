# GRIP BREAK 0.4.12 Windows validation

Native build f191: zero Unity errors/warnings. Current affected checks: 157/157 passed with zero reported runtime issues. Categories: {"physics-snapshot": 8, "momentum": 22, "camera-obstruction": 25, "stamina": 10, "pin-spam": 37, "pin-rules": 18, "hand-contact": 11, "ground-recovery": 26}.

Current reports cover the actual snapshot force/elbow comparison and CPU timing, momentum, camera obstruction, stamina, pin spam/rules, held-hand visuals and pinned ground recovery. Historical 0.4.11 session, shutdown and updater results are retained only after unchanged subsystem code/method fingerprints were verified. They are not presented as new f191 network or quit runs.

Four decorative overhead light assemblies were removed; actual light sources and rendering presets remain. Static batching was rejected and is absent. Whole-scene FPS remains essentially unchanged in recorded tests (73.06 offscreen versus saved 72.70 foreground, different presentation); no improvement percentage or steady-FPS promise is made. Native paired local helper timings show reduced helper CPU cost, which is not a whole-game FPS result.

Root inspected the final native captures listed in its review. Untouched current and historical reports are published in release-evidence/0.4.12. Separate-machine internet acceptance, long human matches and physical-Mac performance remain unverified. Public bytes and the live updater are checked after publication.
