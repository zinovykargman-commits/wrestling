# GRIP BREAK 0.2.0 — build45 validation

Unity 6000.3.7f1 native Windows build succeeded with zero reported errors/warnings. 254 automated assertions passed across fifteen current game reports and seven installer checks, with zero game runtime issues. Source/runtime hashes remained unchanged through validation. Runtime and source archives are CRC-tested and compared to the hash manifest.

## Current behavior checked

Six repeated natural two-hand lifts/throws check release and forward displacement across varied aim directions and lift timing, including releasing Lift before Throw. The overhaul run checks planted idle stance, small-impact stumble, heavy-impact knockdown/recovery, manual upper-body/leg grips, movement against bracing resistance, pull/pivot/shove/throw, escape and regrab protection, filtered camera tracking/obstruction, training modes/reset, F11, difficulty reaction cadence, and connected bodies. Q performs a forward low shot with no automatic grip. The self-collision check measures forearm/hand penetration during stance, shot, grappling, escape and recovery; only directly jointed neighbors are ignored. See overhaul.json for actual distances, errors and penetration tolerance.

Input checks supply held/down states at the normal input polling boundary, exercising mappings through Update/FixedUpdate, movement, all eleven action bindings, rebind capture/reset, F1/Escape and focus handling. This avoids the previous test-input array defect while retaining its regression. It does not inject physical hardware events.

Native camera checks cover Training, AI Local, host/orange and client/blue ownership, one full-window camera, controlled-player centering, opponent independence and resets. Visible menu, Keybinds page, controls and HUD screenshots were inspected. Torso/hips use rounded meshes; the current gameplay captures were inspected for connected bodies and shape.

AI tests cover autonomous commands and connected finite bodies across all three arenas, edge avoidance, pin behavior and passive Training. The AI audit follows replacement wrestlers across legitimate round resets. Easy/Normal/Hard reaction cadence and passive/resisting/attacking partner behavior are separately checked. Automated success does not establish difficulty balance or human enjoyment.

Functional/move tests cover physical locomotion, grabs/lift/throw, shot/tackle, repeated hard collisions, slam, ledge climb, table breaking, tower warning/failure, ducking, pin three-count, scoring/draws/sudden death and resets. The functional run averaged 117.9 FPS at 1920×1080 on Intel(R) Core(TM) i5-14400F / 16173 MB / NVIDIA GeForce RTX 5060; the minimum 5.9 FPS includes synchronous capture/reset stalls. Gameplay.mp4 is the current 18-second camera recording.

Two actual game processes passed private handshake/ready, camera ownership, remote input, body/prop/grip/reset streaming and disconnect behavior with simulated 150-ms RTT and 5% application-message omissions. This is loopback TCP, not separate-PC internet or transport packet-loss evidence.

A separate actual host/client pair used the generated public trycloudflare.com invite and secure WebSockets. Copy Invite placed the full authenticated link on the clipboard; the joining game accepted it and rejected an unrelated host. Public-route handshake, readiness, camera ownership, remote input, body/prop/grip/reset streaming, disconnect and helper cleanup were checked. Both game processes ran on one PC, so separate-PC network environments remain unverified. Invites expire with their host room and temporary service availability is external.

Momentum comparisons use ordinary movement/aim/hand/shot/throw commands and physical measured outcomes: running versus stationary shots, brace resistance, actual rotation before natural manual grabs, matching grip counts for spin throws, repeated release/regrab energy limits and connected finite bodies. See momentum.json for recorded speeds and travel. No test-created grips or externally injected forces are used in those comparisons.

## Updater

Native checks cover version comparison, own-repository manifest restrictions, contained archive extraction, SHA computation and a bounded GitHub check. The installer passed valid install, same-volume backup, forced rollback, corrupt archive, missing/extra/tampered stage rejection, and source preservation. The pre-publication check can legitimately report no release yet. Public download/hash and native update/restart verification are performed after publishing and recorded separately; this pre-publication package makes no claim that a public release was already available.

## Hands-on acceptance remaining

Physical keyboard/controller operation, real controller rebind/rumble/disconnect, full lessons/matches and enjoyment, extended obstruction/navigation cases, separate-PC internet routing, true packet loss and current-build long-session soak need hands-on acceptance. Historical soak reports do not establish those for this build. See MANUAL_CHECKS.md.
