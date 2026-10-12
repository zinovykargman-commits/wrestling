# GRIP BREAK

**[Download the Windows game](https://github.com/zinovykargman-commits/wrestling/releases/latest)** — download GRIP_BREAK-Windows.zip, extract it, and open PLAY.cmd. Future releases install through the main menu's update button.

This repository contains the editable Unity source, update installer, and build instructions. The playable Windows build is distributed as a release asset.

# GRIP BREAK 0.4.6

Extract GRIP_BREAK-Windows.zip and open **PLAY.cmd**. Only **Pin Wrestling** in the ring is active. **PLAY AGAINST AI** and **PRIVATE ONLINE** enter the same rules. First to three pins wins. Training and other rule/arena choices are retired for now.

## Controls

| Action | Keyboard / mouse | Controller |
|---|---|---|
| Move | WASD | Left stick |
| Aim physical hands | Mouse high, low and sideways | Right stick sideways / elevation |
| Left / right grip | Hold left / right mouse; release to let go | Hold LB / RB |
| Cover / pin | Hold C while gripping a grounded rival | D-pad down |
| Brace / escape / bridge / roll | Hold Ctrl and move | Hold LT and move |
| Forward wrestling shot | Q, then manually grab | X |
| Sprawl | Hold E; release to recover | R3 |
| Lift | Hold R | RT |
| Shove | X | D-pad up |
| Sprint | Shift | L3 |
| Jump | Space | A |
| Controls / pause / fullscreen | F1 / Escape / F11 | Keyboard utility keys |

The **KEYBINDS** page changes/saves keyboard, mouse and controller actions. RESET BINDINGS restores defaults. There is no Throw keybind or canned Throw action. Use physical movement and release the clicks to let go with earned momentum.

## Work for a pin

Aim before clicking: high for shoulders, low for legs. Each click holds its own physical hand. A target must be in reach. Moving the cursor with a grip physically works against the target rather than teleporting it.

Pull backward, pivot sideways, or take a forward Q shot and manually grip to work your rival off balance. Hold C while gripping a physically low, lying rival to settle their chest or back onto the mat and use a low controlling stance. The grounded rival's chest/back and both shoulders against the mat, with your live controlling grip, are required for **2.5 continuous seconds**. Shoulder indicators/count show progress. Releasing C, losing control/contact, raising a shoulder interrupts the count. Touching ropes does not interrupt a valid pin. Leaving the ring puts the exited wrestler on hands and knees at center, with the opponent behind. Both stay still for a 1.5-second 3, 2, 1 countdown, then a whistle restores control. Being thrown out still puts the exited wrestler on bottom; scores do not change.

Hold **E** to sprawl: lower your hips, send the legs back and resist a shot with your body weight. Arms and manual grips remain independent; release E to recover. Sprawl drains stamina and cannot turn into a shot while held. Lift uses R; old default lift bindings migrate automatically.

Defend with **Ctrl + movement** to bridge/roll even down. Stamina limits both control and escape. Q never grabs automatically. Faster approaches and actual rotation contribute bounded momentum, brace resists, and regrabbing cannot charge unlimited energy.

Exactly one full-window camera follows orange offline/host or blue when joining. Opponents do not choose the target/zoom. F11 toggles fullscreen.

## Private online

Both PCs need compatible protocol12 builds. Host uses **PRIVATE ONLINE → HOST & MAKE LINK → COPY INVITE** and sends the complete copied link. Friend pastes into Join or presses PASTE INVITE, then JOIN FRIEND. Both ready up; host starts the same pin rules.

The bundled host Cloudflare helper creates a temporary authenticated secure tunnel. Your friend needs no helper installation, router setup, VPN or Cloudflare account. Closing the host room expires its invite. Host physics streams bodies, grips, shoulders and pin state; client sends movement and 3D hand targets. Protocol12 rejects incompatible builds. Temporary service availability is external.

## Update and source

Use **CHECK FOR UPDATE**, then **UPDATE & RESTART** when a newer release is available. The game verifies own-repository version/URL, size and SHA256, stages a contained archive and runs its bundled installer. Backup/rollback and restart remain; preferences and editable Source are preserved. Extract to a writable folder.

Open Source in Unity **6000.3.7f1** with Windows Build Support (Mono). Editor method **BuildGripBreak.Build** creates Build/GRIP_BREAK.exe. Keep Network and Update beside Build. GBWrestlerArms.cs owns arm effort, GBWrestlerPin.cs owns cover/shoulder/ground defense, GBApp owns rules/menu, GBNetwork owns host-authority transport.

Current audits: --pin-test, --input-test, --camera-test, --updater-test, --record and --menu-shot with --evidence. Network: --host-test / --client-test, matching --port, or --tunnel for public copied invites. VALIDATION.md records current evidence; MANUAL_CHECKS.md covers hardware, human feel and separate-PC acceptance. Earlier Evidence reports are historical.

Stamina capacity is155.25 (+15% over the previous135). Ctrl movement scramble effort costs30% more; other action costs are unchanged. Strength and the meter use the new capacity.

Walking uses alternating physical foot targets and coordinated thigh/knee motors, with raised foot clearance and knee straightening on landing. No body animation teleports; idle and special wrestling stances remain separate.

Current pin rule: the grounded wrestler may be chest-down or back-down, with both actual shoulders on the mat and a live nearby controlling grip while C is held. The attacker does not need to place their chest over the rival. Multiplayer omits the mat motivational slogan and bottom coaching prompt.

Tackle has been removed from keyboard/controller bindings, control legends, AI decisions and physics. The retired network input bit is rejected. Windows 0.4.6 retains protocol12. Mac updates are paused at0.4.5; its older countdown display/audio remain unchanged.
Jump requires 8 stamina (climb 16) and is unavailable while under pin control. C requires a physically low, lying opponent before pin control; low balance or a Down timer while upright is insufficient. Ctrl + movement still bridges/rolls.


0.4.0 physical control improvements: Q connects through actual torso/hip collisions and briefly disrupts neutral footing; Ctrl or E can resist. A running jump carries your actual velocity into a single body impact, without automatic grips or a second attack impulse. Pulls/pivots have full opposite reactions. Hold C and move toward a grounded rival's left/right shoulder to shift existing pin pressure. Neutral pin pressure is preserved. Separate L/R aim markers show each hand's feasible reach; outlined body regions show actual held grips. Elbows and wrists use stable anatomical targets through physical motors. No additional escape mechanic was added.
Single-grip pin control is stronger in 0.4.0 so a live one-hand hold can settle a raised shoulder. Two-hand control, real shoulder requirements and the three-count are unchanged.

0.4.2: slightly more forgiving general grip break limits, without stronger pulling forces or higher stamina costs. Carried/released bodies preserve physical orientation through flight and briefly settle on impact before automatic standing recovery; tilted releases can land on their core instead of always on boots.

0.4.3: pins against ropes count for2.5 seconds; active lift/pivot/slam/shove leverage reduced slightly, with weaker one-arm, grounded and pinned leverage. Scramble costs an additional10% and has1.2 seconds recovery after usable stamina runs out.

0.4.4: opponent bracing no longer drains your stamina. Holding costs 2 stamina per hand per second; lifting adds 26/s. Bracers still pay their own costs and resist physically.

0.4.5: scrambling never auto-releases the opponent's grips; actual movement/strain determines escape. Ctrl release has0.4s repress recovery to prevent rapid-tap bursts; exhaustion stays1.2s.

0.4.6: offender-bottom ring-out restarts, 1.5-second 3/2/1 and referee whistle. Ordinary match starts remain three seconds. Mac source/builds are unchanged.
