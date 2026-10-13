# Rejected f179 audit candidate

The untouched f179 report set contains 19 JSON reports with 413 assertions: 410 passed, three failed, and all reports record zero runtime issues. This is historical evidence; it must not be relabelled as the final release result.

`standing_ctrl_lowers_physical_hips` measured the real hips from 1.241782 m to 1.112082 m, a 0.129700 m drop. That missed the fixture's arbitrary 0.130000 m minimum by 0.000300 m. The native stance image was inspected and showed a supported lower stance. Root approved a final minimum of 0.125 m, explicitly allowing 0.005 m of solver/contact variation around the 0.13 m reference. Independent bent-knee, head/chest orientation, boot support and free-standing stamina checks remain unchanged. No stance gameplay force or target was changed for this tolerance correction.

The two other f179 failures were the historical scripted cases `standing_manual_exchange_reaches_pin` and `walking_moves_with_alternating_physical_steps`. Natural pin setup varies with physical outcomes; no report is converted to a pass. Actual final reports decide whether one or both remain.

The f178 network client previously exposed a shoulder-render joint access issue. Root cached the authored chest shoulder anchor before clients remove local physics joints. Fresh f179 host/client reports passed 32 assertions with zero runtime issues; physical bodies, protocol and fields were unchanged. The failed f178 client evidence is preserved.

Raw reports remain in `work/checks/f179-*` and `work/checks/f178-net`. The subsequent f180 candidate must be built, frozen, tested and inspected separately before publication.
