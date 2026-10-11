# GRIP BREAK

**[Download the Windows game](https://github.com/zinovykargman-commits/wrestling/releases/latest)** — download GRIP_BREAK-Windows.zip, extract it, and open PLAY.cmd. Future releases install through the main menu's update button.

This repository contains the editable Unity source, update installer, and build instructions. The playable Windows build is distributed as a release asset.

# GRIP BREAK v0.2.0

A Windows physics wrestling game for AI Local, Private Online and Training. Operate each hand yourself, pull opponents off balance, and turn momentum into a throw. The wrestlers have fifteen physical body regions, permanent body joints, rounded torso and hip shapes, and continuous connections.

## Play

Double-click **PLAY.cmd**. Keep the complete game folder together, including Build, Network, Update and the launcher. Unity is not needed to play. The launcher opens a 1920×1080 window; **F11** toggles fullscreen.

Choose **AI LOCAL** for a computer opponent, **PRIVATE ONLINE** for a friend on another PC, or **TRAINING** for practice. Every mode uses one full-window camera following your wrestler. There is no split screen or second local human input.

The main menu's **YOUR INPUT** button selects keyboard/mouse or one XInput controller. Missing or disconnected controllers fall back to keyboard/mouse. Open **KEYBINDS** to select keyboard/mouse or controller bindings, click an action and press its replacement input. **RESET BINDINGS** restores defaults. Controller sticks remain the move/aim axes; keyboard movement keys can be changed. F1, Escape, F11 and T are fixed utility keys. Options controls volume, camera shake and rumble.

Press **F1** or the in-match controls button for your saved control legend. Escape opens the pause menu. Offline focus loss pauses play and clears held inputs; returning resumes an automatic focus pause, while an intentional pause stays paused.

| Action | Keyboard / mouse default | Controller default |
|---|---|---|
| Move / aim | WASD / mouse position | Left / right stick |
| Left / right grip | Hold left / right mouse | Hold LB / RB |
| Sprint | Left Shift | Left stick click |
| Jump / climb | Space | A |
| Lift | Hold E | Hold RT |
| Throw | F | Y |
| Forward wrestling shot | Q | X |
| Shove from a grip | X | D-pad up |
| Brace / escape | Hold left Ctrl | Hold LT |
| Tackle | R | B |
| Legs / pin | Hold C | Hold D-pad down |
| Controls / pause | F1 / Escape | Keyboard F1 / Escape |
| Fullscreen | F11 | Keyboard F11 |
| Training drill reset | T or RESET DRILL | Click RESET DRILL or keyboard T |

## Wrestling

**Q performs a forward wrestling shot. It does not grab automatically.** Use either mouse button afterward to grab manually. A shot lowers your stance and drives you forward; contact and opponent brace affect the result.

Hold one hand for a loose grip, or both for more control. A nearby target ring shows a reachable body region; grips still require physical hand contact and a clear path. Default targets are upper-body regions. Hold **C** while grabbing to target legs outside a pin match. In pin matches C keeps its pin action.

Walk backward while holding to pull, step sideways to pivot, or drive forward to move an opponent. **X** shoves from a manual grip, releases the hands and creates space. Hold E to lift, then aim and press F to throw. Moving and rotating before contact contribute momentum. Fast shots transfer more forward momentum on contact; spinning into a manual grip carries tangential motion into your opponent. Brace reduces the transfer. The gain is bounded, so repeated grip tapping cannot charge an unlimited boost. Release hand buttons before acquiring another grip after a throw or shove.

Hold Ctrl and move while an opponent holds you to charge an escape. A successful escape creates a short opening before another grab can attach. Brace also resists lifts and attacks, using stamina. Lift plus brace drives a slam. Catch a high ledge, maintain a grip and press Space to climb. R performs a tackle; a missed tackle leaves a brief opening.

The HUD shows stamina, balance, selected target region and grip strain. Action messages explain out-of-reach attempts, exhausted actions, braced opponents and successful escapes. Movement uses planted-foot support and controlled recovery; impacts can still knock you down.

## Your camera

The camera follows orange in AI Local, Training and online host play, and blue when joining online. It filters body wobble with quicker horizontal tracking and slower vertical tracking. Obstacles pull the camera inward; corner checks can choose a clearer side, with a smooth return after clearance. Your opponent never becomes the target or determines zoom. Mouse aim and movement use your own view.

## Modes and places

Choose **EASY**, **NORMAL** or **HARD** on the main menu for AI Local. Difficulty changes reactions, openings and tactics; it does not give extra strength or stamina. Easy leaves longer openings. Hard reacts faster and uses pull, pivot, shove and leg-target decisions. AI uses the same physical commands as a human and probes the floor and obstacles to avoid hazards.

**Ring-out** is first to three rounds, with 120-second rounds and 30 seconds of sudden death. Eliminations within a quarter second produce a draw. Rematches restore arena state and scores. Exactly three competitive arenas are included:

- **Freight Train:** two freight cars, cargo, breakaway railings, moving scenery and low overhead beams. Brace to crouch; beams descend in sudden death.
- **Furniture Store:** movable stock, furniture, breakable tables, a loading pit and storefront exits. Red lines show the shrinking sudden-death boundary.
- **Crumbling Tower:** a central roof, weak outer platforms, scaffolding and a swinging sign. Orange cracks warn six seconds before scheduled collapse.

**Pin Wrestling** uses the separate gym. Hold a grip and C while controlling an opponent with both shoulders near the mat. Shoulder, grip, range and rope changes interrupt the three-count. Brace to raise a shoulder. First to three pins wins; leaving the ring resets the round.

**Training** has no score or timer. Use the **PARTNER** button to cycle **PASSIVE**, **RESISTING** and **ATTACKING**. Passive stays available for drills; resisting braces and makes gentle escape steps when held, with periodic openings; attacking uses the selected AI difficulty. The separate brown dummy remains passive. The gym also contains physical ropes, props, a ledge drill and six lessons. Press **T** or **RESET DRILL** to restart practice immediately.

## Play with a friend online

Both PCs need the same build. Choose **PRIVATE ONLINE**. The host presses **HOST & MAKE LINK**, waits for the invite, then **COPY INVITE** and sends that full link to the friend. The friend presses **PASTE INVITE** (or pastes into the field), then **JOIN FRIEND**. Both press READY; the host chooses rules and arena and starts.

The host's bundled Cloudflare helper creates a temporary public tunnel. The friend needs no helper installation, port forwarding, VPN or Cloudflare account. The invite includes the room's random authentication token; share it with your friend. Keep the host room open: leaving it closes the tunnel and ends the invite. A new room creates a new link. Temporary tunnels have no uptime guarantee; if the connection fails, return to the menu and create a fresh room.

Only the host simulates physics; the client sends inputs over secure WebSockets and receives interpolated bodies, props, grips, hazards and match state. The local bridge binds only to loopback. The direct TCP API remains for development/LAN audits, while the player menu uses invites.

Current native public-route evidence is documented in VALIDATION.md. Two game processes on one PC do not establish separate-PC input hardware, every network environment or two-person play feel. See MANUAL_CHECKS.md for acceptance.

## Updates and source

The game targets [zinovykargman-commits/wrestling](https://github.com/zinovykargman-commits/wrestling), with this source version set to **0.2.0**. In the main menu press **CHECK FOR UPDATE**. If a newer published release is available, the button becomes **UPDATE & RESTART**; press it again to download and install. Checking alone does not close the game.

The updater compares the release manifest, version, archive size and SHA256, checks staged files against the archive, and uses the bundled local installer. It closes the game, backs up existing files on the installation drive, installs and restarts. Failed file replacement attempts restore the previous files. Update logs remain under the game's persistent-data updates folder; retained backups are named .grip-update-backup-* inside the installation. Keep the installation in a folder your account can write. Saved preferences live outside the installation, and runtime updates preserve editable Source files. Availability depends on a published GitHub release and internet access; this README is not proof that a release has been published or an update has completed.

Open **Source** in Unity **6000.3.7f1** with Windows Build Support (Mono). The game uses the built-in renderer, Unity/PhysX, framework TCP/WebSocket sockets, a bundled official cloudflared helper and UnityWebRequest. Characters, arenas and synthesized sounds are authored locally.

Build from the source project using the editor method **BuildGripBreak.Build**. It writes Build/GRIP_BREAK.exe and build-status.txt in the game folder. Keep the bundled Network folder alongside Build to host invites. GBWrestler.cs owns bodies and movement, GBWrestlerCombat.cs and GBWrestlerMomentum.cs own grappling/moves/momentum, GBBodyVisual.cs owns rounded body shapes, GBPlayerCameraRig.cs owns follow framing, GBBot.cs owns AI/training, GBApp.cs owns UI/rules, GBNetwork.cs and GBTunnel.cs own private invites/transport, and GBUpdater.cs owns release checks and staging.

## Validation

The runtime supports --momentum-test, --throw-test, --input-test, --ai-test, --camera-test, --overhaul-test, --updater-test, --audit, --moves, --soak and --record with an absolute --evidence folder and -logFile path. Host/client checks use --host-test and --client-test with a matching --port; add --tunnel and a shared absolute --evidence folder to test the generated public invite instead. Matching --delay and --drop values simulate latency and application-message omissions; TCP message omissions are not actual transport packet loss.

VALIDATION.md identifies the exact tested build, reports and limits. Prior-build evidence is historical after source changes. Automated inputs and captures cannot establish human enjoyment or physical keyboard/controller compatibility. See LICENSES.md for engine and authored-content notices.
