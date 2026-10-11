# GRIP BREAK 0.4.2

Stamina capacity is 155.25, an additional 15% over 135, with action costs unchanged. Both online players must update to 0.4.2 (protocol 10), because old clients reject the higher capacity.

General non-ledge grips allow about 8% more anchor separation and 10% more computed overload before accumulating slip strain, plus 20ms additional overload grace. The applied force cap, reciprocal reactions, stamina cost, exhaustion, manual release, opponent escapes and ledge limits are unchanged. Sustained overload still breaks a grip.

A wrestler carried off their feet no longer applies the standing height/upright/foot servos in flight. After release, real core or boot contact permits recovery after 0.35 seconds. This preserves the body's earned orientation/momentum; no random knockdown, added throw impulse or Throw action is introduced. Upright releases can still land on boots.

Fresh native Windows build succeeded with zero errors/warnings. Current native momentum, carry, input and same-PC protocol10 checks passed 103/103 checks with zero runtime issues. Failed observations: none. Landing fixtures deliberately prepare upright/tilted real airborne holds, release through ordinary input, and verify floor contact, preserved tilt and eventual recovery without a stuck state. They isolate recovery; they do not prove every human lift/throw combination. Momentum checks use actual commands to verify spin/grab transfer, running shots/jumps, repeated energy limits and release. Current landing screenshots were inspected.

0.4.1's broader pin scenario failures remain unresolved; the raw report is retained as historical evidence. This patch does not claim a full pin/network regression or human balance acceptance. Current source/runtime identities and packaged ZIP hashes/CRCs were verified. Public download/updater checks follow publication.
