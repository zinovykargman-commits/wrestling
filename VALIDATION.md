# GRIP BREAK 0.3.2

Fresh native build succeeded with zero errors/warnings. 46 assertions passed across three current reports with zero runtime issues; frozen source/runtime identities matched before packaging.

Regression checks verify that hostname assignment alone does not advertise an unregistered route, registration immediately publishes an authenticated invite without needing the host's local DNS resolver, the first assigned hostname is preserved, and late callbacks cannot reopen a closed room. Existing updater checks passed.

Two native peers then used a real public quick tunnel, the actual Copy Invite clipboard value, pasted Join, authentication, ready/start, protocol 9 inputs/body/grip/reset streams, owned cameras and disconnect. Actual host invite screenshot was inspected. This route ran on one PC; separate-PC routing is still a hands-on check. The joining peer retains normal secure DNS/TLS retries, and a stale recursive DNS cache delayed this test's connection before it succeeded. No system DNS settings or TLS certificate checks were changed.

Gameplay guard/input/menu checks passed 69 assertions in 0.3.1 and are historical; full pin/walking checks passed in 0.3.0. Only invite readiness changed in 0.3.2. Archives are CRC-tested and match frozen source/runtime identities. Public asset verification follows publication.
