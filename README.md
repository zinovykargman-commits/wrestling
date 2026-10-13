# GRIP BREAK

**[Download the Windows game](https://github.com/zinovykargman-commits/wrestling/releases/latest)** — download GRIP_BREAK-Windows.zip, extract it, and open PLAY.cmd. Future releases install through the main menu's update button.

This repository contains the editable Unity source, update installer, and build instructions. The playable Windows build is distributed as a release asset.

# GRIP BREAK 0.4.9

Extract **GRIP_BREAK-Windows.zip** and open **PLAY.cmd**. No Unity installation is needed to play. **PLAY AGAINST AI** and **PRIVATE ONLINE** use the same Pin Wrestling rules: first to three pins wins. There is one full-window camera locked to the wrestler you control. Training, split screen and other modes are inactive.

## Controls

| Action | Keyboard / mouse | Windows controller |
|---|---|---|
| Move | WASD | Left stick |
| Aim physical hands | Mouse high, low and sideways | Right stick / elevation |
| Left / right grip | Hold left / right mouse; release to let go | Hold LB / RB |
| Cover / pin | Hold C with a grip on a grounded rival | D-pad down |
| Lower stance / brace / ground escape | Hold Ctrl; move to work position | Hold LT and move |
| Forward wrestling shot | Q, then manually grip | X |
| Sprawl | Hold E; release to recover | R3 |
| Lift | Hold R | RT |
| Sprint | Shift | L3 |
| Jump | Space | A |
| Keybinds / pause / fullscreen | F1 / Escape / F11 | Keyboard utility keys |

The **KEYBINDS** page changes and saves keyboard, mouse and controller mappings. RESET BINDINGS restores defaults. Tackle, X shove and the old Throw action have been removed. Throwing follows actual movement, lifting and release of your manual grips.

## Wrestle for position

Aim before clicking: high for shoulders, low for legs. Each button holds its own physical hand. A target must be within real reach. Held hand movement works against the rival through the physical grip. A small visual fit brings a near-contact hand closer to an already-held body; it does not create a grip, add reach or apply force.

While freely standing, **Ctrl** lowers your hips into a supported wrestling stance with the head up. Release to stand normally. Free standing stance does not spend scramble stamina. When held by the rival or down on the mat, Ctrl retains paid defensive bracing/bridge/roll behavior. Exhausted ground scramble has a 1.2-second recovery gate, and rapid release/repress cannot create repeated bursts. Scrambling does not automatically remove the opponent's grips; real strain can break them.

**Q** uses a slightly lower level change, leading knee/foot entry, trailing-leg drive and recovery. It never grabs automatically. **E** sprawls the hips down and legs back to resist a shot while keeping the arms independent. Release E to recover. Faster approaches, jumps, pivots and spins carry bounded earned momentum into contact and held movement.

With a real manual grip on a grounded rival's leg, move sideways to turn the corner and improve position. Resistance matters; one hand gives less leverage than two. This movement does not finish a takedown or pin automatically.

Hold **C** with live nearby control of a physically low, lying rival. Top position settles your hips and distributes physical shoulder pressure. The grounded rival's chest or back and both actual shoulders must contact the mat for **2.5 earned seconds**. The attacker does not need their chest on the rival. Missing shoulder contact earns no progress; brief twitches pause the count, and sustained escape, release of C, lost grip or lost control interrupts it. The bottom player can physically bridge/roll and escape. Ropes do not cancel a valid pin.

Going over the ropes restarts the exited wrestler on hands and knees, with the opponent kneeling behind in top position, even when they were thrown out. A rapid 1.5-second 3, 2, 1 countdown and whistle restore control. Scores remain unchanged.

Stamina capacity remains **155.25**. Holding costs 2 per hand per second; lift adds 26/s. The rival's bracing never adds drain to your stamina. Ground scramble remains 1.43 times its original effort cost. Jump requires 8 stamina, climbing 16; exhausted jumping and jumping under active pin control are blocked. Repeated Ctrl attempts postpone free recovery, and active pin control blocks regeneration.

## Graphics and appearance

**OPTIONS** offers **HIGH**, **BALANCED** and **PERFORMANCE** rendering. Windows defaults to High; Mac defaults to Balanced. These presets change rendering rather than physics, contacts or network state. The menu camera now stays inside the roofed gym.

The realistic athletes use licensed skin, hair and eye textures, fitted cloth singlets, attached matte boots and continuous anatomy driven by the existing physical bodies. Shoulder skin weights, volume blending and elbow routing have been refined and reviewed in native front/back/side, raised-arm and live-reach views. Extreme clothing folds and some small trim wisps remain; see KNOWN_ISSUES.md. No claim is made that every possible pose is anatomically perfect.

## Private online

Use matching **0.4.9** builds on both computers. Protocol 12 retains host authority and the same 92 registered bodies. Windows hosts: **PRIVATE ONLINE → HOST & MAKE LINK → COPY INVITE**. Send the complete copied invite to your friend. They paste it into Join, select **JOIN FRIEND**, ready up, then the host starts.

The bundled Windows Cloudflare helper creates a temporary authenticated tunnel without router setup, VPN or a Cloudflare account. Closing the room expires the invite. The game streams existing bodies, grips, shoulder contacts and pin state. Mac joins a Windows-hosted invite; native Mac hosting/controllers are outside the current Mac package. Current separate-machine internet play and physical-Mac performance require hands-on verification. Temporary service availability is external.

## Updates and editable source

Windows: use **CHECK FOR UPDATE**, then **UPDATE & RESTART** when a newer release is available. The game verifies repository/version/URL, size and SHA256, stages a contained archive, backs up the old runtime and restarts. Preferences and editable Source are preserved. Extract into a writable folder. Mac's update button opens the Mac release download; replace the extracted app manually.

Windows downloads: https://github.com/zinovykargman-commits/wrestling/releases/latest

Mac downloads: https://github.com/zinovykargman-commits/macoswrestling/releases/latest

The Source archive contains Unity Assets/Packages/ProjectSettings, licensed runtime mesh/texture assets, the installer/network helpers and an editable packed Blender master under **Authoring**. Open Source in Unity **6000.3.7f1** with the platform's build support. **BuildGripBreak.Build** creates the native player. Keep Network and Update beside the Windows Build. The authoring exporter is pipeline source, not a standalone command; see Authoring/README.md.

Windows build **f180** passed **412 of 413 native assertions**, with zero build errors/warnings and zero runtime issues. All new feature checks, the dedicated 20-check locomotion suite, the manual standing pin exchange and 32 host/client checks passed. One older walking fixture still failed its boot-height criterion; the untouched failure remains in the evidence. VALIDATION.md records the category results.

Untouched report JSON and inspected key captures are published in the [repository release evidence](https://github.com/zinovykargman-commits/wrestling/tree/main/release-evidence/0.4.9). Test screenshots, videos and reports are excluded from the playable ZIP. MANUAL_CHECKS.md separates hardware, human feel and independent-network checks. Public archive byte verification and the live updater check are recorded after publication in the repository. Automated evidence does not establish long-session or human-balance acceptance.
