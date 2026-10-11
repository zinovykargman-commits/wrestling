# GRIP BREAK acceptance ledger

This ledger preserves the original scope, subsequent corrections and the authorized overhaul by exactly five agents total: one integration lead and four specialists. Source implementation, current packaged-runtime verification and human acceptance are separate. **VALIDATION.md owns the exact test results and build identity**; this ledger does not claim that tests or GitHub publication are finished.

| Area | Acceptance requirement and implementation |
|---|---|
| Delivery | Windows executable, editable Unity 6000.3.7f1 source, PLAY.cmd, instructions, licenses, screenshots, recording and SHA256 package manifests. Current artifact presence and hashes require final delivery verification. |
| Entrances | Exactly AI Local, Private Online and Training. One human versus AI offline; two remote human competitors online; one controlled human in Training. |
| View quantity | Exactly one full-window camera in every mode, including Training. No split screen and no second local human input. |
| Camera ownership | Controlled orange wrestler offline/host, blue wrestler for a client. Opponent/dummy never drives framing. Wobble filtering, stable aiming focus, obstacle checks and side selection are implemented; extreme cases require hands-on acceptance. |
| Connected bodies | Fifteen physical regions, permanent anatomical joints, bounded spring hand grips, rounded torso/hips and continuous collars/seams. Native integrity measurements and inspected renders are separate evidence requirements. |
| Self collision | Only directly connected anatomical pairs ignore each other. Nonadjacent hands, limbs and torso use physical contacts. Persistent clipping/unstable self contact must be evaluated in current runtime and renders. |
| Movement | Planted-foot support, responsive finite-force movement against opponents, turning, small-impact stumbling and reliable recovery. Resistance, repeated contact and recovery need behavioral checks. |
| Grappling | Independent manual hands, physical reach/contact, obstruction checks, upper-body/leg targeting, visible target rings, two-hand control and grip strain. C selects legs outside pin rules and performs pin within pin rules. |
| Forward shot | Q / controller X performs a forward wrestling shot with stance and momentum. It never acquires a grip automatically; hand controls remain manual. |
| Shove | Keyboard X / controller D-pad up shoves from a manual grip, releases hands and creates space. |
| Moves | Push/pull, lateral pivot, lift, momentum throw, shot, tackle, lower-limb trip, slam, ledge catch/climb, pin and brace/escape remain implemented. Distinct input/action behavior needs current checks. |
| Counterplay | Brace costs stamina and resists attacks/lifts. Direction plus brace charges escape and creates a brief period against immediate recapture. Missed tackles expose the attacker. Subjective competitive balance requires playtesting. |
| Feedback | Stamina, balance, target mode, grip strain and messages for reach, brace, exhaustion, recovery and escape. HUD readability requires actual window inspection. |
| Keybinds | Dedicated KEYBINDS page for keyboard/mouse and controller action rebinding, saved preferences and RESET BINDINGS. Keyboard movement keys can change; controller sticks remain axes. |
| Utility controls | F1 saved-controls legend, Escape pause, F11 fullscreen, T quick Training reset. These are reserved utility keys. Controller disconnect falls back to keyboard/mouse. |
| Input integrity | Runtime-only test inputs cannot override normal polling through serialized empty arrays. Buffered presses and offline focus recovery retained. Physical hardware/rebinding acceptance remains explicit. |
| AI | Easy/Normal/Hard alter reactions, openings, stamina decisions and tactics through ordinary commands, without strength bonuses. Edge/hazard steering retained. Private Online never substitutes local AI for the remote player. |
| Training | Passive, resisting and attacking blue partner choices, separate passive brown dummy, no score/timer, six lessons, immediate T/button reset, physical ropes, ledge and props. |
| Arena quantity | Exactly three ring-out arenas plus a separate gym used for Training and pin rules. |
| Train | Freight decks, connection/gap, cargo, breakaway rails, moving scenery, beams and crouch warnings; descending sudden-death beams. |
| Store | Furniture, shelves, counter, movable stock, breakable tables, marked pit, storefront exits and shrinking sudden-death boundary. |
| Tower | Central roof, weak outer sections, scaffold, swinging sign, six-second collapse warning and closing sudden-death boundary. |
| Rules | First to three ring-outs or pins; 120-second rounds, quarter-second simultaneous draw window and 30-second ring-out sudden death. Reset/rematch restore state. |
| Pin | Controlled shoulders-down three-count; shoulder, grip, range and rope interruptions; first to three; out-of-ring reset. |
| Network | One-click host creates a temporary public invite with random room authentication; Copy Invite and paste-to-Join, ready states, selections, match transitions, disconnect handling, sequenced input, host physics authority and interpolated state. |
| Internet | Bundled official cloudflared host helper and secure WebSocket client; no friend helper/VPN/router/port setup. Verify an actual public tunnel route; separate-PC environments and true packet loss need external acceptance. |
| Momentum | Earned movement/spin momentum transfers through manual grips, shots and throws; faster approaches have measurable advantage, brace resistance, finite carry and no repeated-grab energy farming. Q remains manual-grip-free. |
| Updater | Main-menu CHECK FOR UPDATE followed by UPDATE & RESTART for a newer published release. GitHub manifest version/URL/size/SHA verification, safe extraction, staged-byte checks, local helper, same-volume backup, rollback and restart are implemented. Release availability and successful update need separate verification. |
| Repository | Requested GitHub destination is https://github.com/zinovykargman-commits/wrestling with source version 0.2.0. Publication is an external action and is not inferred from local source or this ledger. |
| Presentation | Original bodies, menus, sounds, lighting and distinct arenas. Rounded torso/hip geometry, camera comfort and HUD must be inspected in actual native captures. |
| QA/performance | Final source/build hash consistency, current runtime checks, actual screenshots and measured performance. Earlier reports and soaks do not automatically apply to changed source. |

Remaining acceptance includes physical keyboard/controller and rumble, complete tutorial walkthrough, human wrestling feel, sustained competitive matches, unusual obstruction/contact cases and separate-PC internet play. MANUAL_CHECKS.md provides concrete checks.
