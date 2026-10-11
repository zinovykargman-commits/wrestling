# Hands-on acceptance for v0.2.0

Automated tests inject commands or key states at the polling boundary. They do not establish physical keyboard/controller operation, human enjoyment or separate-PC internet routing. Consult VALIDATION.md for what the current artifact actually passed.

1. Launch PLAY.cmd. Confirm the only play entrances are AI Local, Private Online and Training. Every mode must show one full-window camera following the controlled wrestler, including after reset/rematch. Online host follows orange; joining client follows blue.

2. In Training, test physical WASD, mouse aim/buttons, Shift, Space, E, F, Q, X, Ctrl, R and C. Hold hands for manual contact grips. **Q must drive a forward shot without automatically grabbing.** Grab afterward manually. **X must shove from an existing grip**, release it and create space. Outside pin rules C chooses leg targets; release C for upper-body targets.

3. Test F1 open/close, Escape pause/resume, F11 fullscreen/windowed and T Training reset. Reset should return bodies, stamina and drills to a usable state. Alt-tab away and return: an automatic focus pause should resume; intentional pause should remain.

4. Open KEYBINDS. Change a keyboard action and a movement key, save, restart and verify input plus F1 legend. Test controller action rebinding separately. RESET BINDINGS must restore keyboard/mouse and controller defaults. F1/Escape/F11/T remain utility keys. Check mouse button capture and unused-key choices.

5. Use one physical XInput controller: both sticks, both hands, lift, throw, shot, shove, brace, tackle, legs/pin, sprint and jump/climb. Test disconnect fallback and rumble. A source mapping or synthetic input is not hardware acceptance.

6. Try moving, turning and sprinting against a resisting partner. Check foot support and ability to make progress without constant falls. Compare standing shots with shots after a sprint, and standing grabs with grabs immediately after a hard spin. Momentum should carry into the rival; brace should reduce it. Repeated release/grab tapping must not build unlimited speed. Small bumps should stumble; committed hits/lost balance may knock down. Check recovery without an immediate repeated knockdown loop.

7. Practice manual upper-body and leg grabs while watching target rings. Try pull, pivot, lift, throw, shove, slam, escape and ledge climb. Read balance, stamina, grip strain and failure messages. Confirm brace slows attacks at a stamina cost and escape leaves a brief opening against instant recapture.

8. Cycle Training partner PASSIVE → RESISTING → ATTACKING. Passive should not initiate attacks; resisting should defend/step when held without attacking and should leave openings; attacking should use the selected AI difficulty. The brown dummy remains passive. Try T/button reset with each partner and complete all six lessons.

9. Play Easy, Normal and Hard AI ring-out matches on all three arenas and a pin match. Easy should offer longer reaction/opening windows; Hard should make faster tactical choices without extra physical strength. Check pursuit, grabs, shot/shove separation, hazard steering, scoring, sudden death and full rematches.

10. In pin rules, use C with a held opponent and both shoulders near the mat. Test interruptions by lifting a shoulder, releasing a grip, moving away and touching ropes. Test first-to-three scoring and leaving-ring reset.

11. Watch neck, waist and every limb during pulls, throws, hard landings and reciprocal grabs. Rounded torso/hips and collars should remain continuous. Nonadjacent limbs should make physical contact; inspect clipping and self-contact stability during recovery.

12. Test the camera near walls, furniture, low beams and ledges, while lifting/falling and during online movement. Check the wrestler remains visible, wobble stays controlled, corners do not cause rapid oscillation, and mouse aim still agrees with the view.

13. Play Private Online on two PCs with the same build. Host presses HOST & MAKE LINK, then COPY INVITE; friend pastes the full link into Join and presses JOIN FRIEND. No router/VPN/port setup should be needed. Both ready up. Test arena/rules, reciprocal holds, rematches, malformed invites, disconnect and a fresh invite after reconnecting. Closing the room must close its temporary tunnel. A public-route test on one PC does not establish all separate-PC environments. Use external transport impairment for actual packet-loss acceptance.

14. On a writable installation, press CHECK FOR UPDATE. An up-to-date release should report that status. A newer release should offer UPDATE & RESTART, with checking alone leaving the game open. Press again, confirm download/verification, close/restart and new version. Check saved settings and editable Source remain. Installer success, rollback/rejection and restart are separate checks; isolate failure tests in a disposable game copy.

15. Evaluate camera comfort, sound, enjoyment and frame performance on both players' hardware. Complete matches and friendly two-person play remain the acceptance standard for fun and balance.
