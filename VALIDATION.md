# GRIP BREAK 0.4.1

Removed the large floating hand description panels (REACH, HOLD, MAX REACH, AIM). Small independent L/R aim markers, held-region brackets and normal HUD remain. Gameplay is unchanged from 0.4.0.

Fresh native Windows build succeeded with zero errors/warnings. The actual hand HUD screenshots were inspected. The existing native pin regression passed 67 of 71 checks with zero runtime issues on this build. Failed scenario observations: three_second_physical_pin_wins, pin_counts_without_attackers_chest_cover, rope_interrupts_actual_live_count, standing_manual_exchange_reaches_pin. These full physics scenarios are not the UI acceptance check; their raw results are preserved, and no physics fix or full gameplay pass is claimed by this cosmetic patch. Source/runtime hashes were frozen and archive contents matched them; both ZIPs passed CRC checks. The broader gameplay/network tests in 0.4.0 are historical. Public assets/updater checks follow publication.
