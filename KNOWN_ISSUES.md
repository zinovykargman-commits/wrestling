# Known visual issue

When reaching both arms overhead while pitched forward, shoulder/upper-arm skin can crease or pinch unnaturally. The user explicitly deferred this deformation for the next update and requested shipping the current game. The limbs remain joined physically; this is an unresolved visual skinning issue, not a completed fix.

The blue wrestler's arm can also look crooked in the relaxed stance. The user separately marked that arm alignment/shape issue for the next pass. Both supplied references are preserved locally in work/next-pass-arm-references; no arm fix is claimed in this release.

Two scripted gameplay scenarios remain inconsistent in the final build: the18-second automatic shot/pull/pin sequence sometimes never grounds both shoulders, and an older walking fixture did not detect both boot centers rising4.5cm. Their failed reports are included. The dedicated20-check movement suite passed, as did actual manually acquired grounded pins, physical escapes, pin guards, stamina and32 host/client checks. Some natural pin setups and walking presentation still need human playtesting/polish; this is a build to try, not a claim that every scenario passed.

A tiny light seam wrinkle may remain behind the shoulder in extreme raised poses. The editable Blender master reopens with all eight textures packed, but its static Cycles hair shading differs from the native game. Native screenshots are the evidence for the shipped characters.

Mac updates remain paused at0.4.5. Separate-machine internet play, long sessions and human balance acceptance have not been rerun for this Windows release.
