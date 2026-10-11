# Public release verification

Result: PASS.

The verification fetches the latest release and its assets anonymously from GitHub. It compares the actual public Windows ZIP with the public update manifest and the current local delivery ZIP, then uses the trusted bundled installer in an isolated installation and restarts the downloaded Windows game.

- PASS: public_latest_version — v0.2.0
- PASS: public_assets_ready — manifest and Windows ZIP from expected public repository
- PASS: public_manifest_matches_release — version, URL, asset size, and SHA-256 format verified
- PASS: public_archive_matches_manifest — anonymous download: 143461756 bytes, SHA-256 3c426865a181e7f7283c139a1a3735529491abdd7474738ebf4cf5e9d7c5779c
- PASS: public_archive_matches_local_delivery — actual public bytes match current local Windows ZIP
- PASS: public_source_asset_ready — editable source ZIP is present in the public release
- PASS: public_source_archive_matches_local_delivery — actual public source archive SHA256 matches the curated local source delivery
- PASS: public_source_archive_crc_valid — public source ZIP decompresses correctly and contains editable game/network sources
- PASS: trusted_installer_succeeded — exit=0; bundled helper SHA-256=5f7945aa02bf59bb8267ec1d9a10ee69302971ab19c3ac3026a42c0d161b4b87
- PASS: public_game_restarted_and_finished — installer-launched public Windows game wrote native UI report
- PASS: public_native_checks_pass — checks=2; issues=0; build=0.2.0
- PASS: installed_assembly_matches_public_archive — Assembly-CSharp.dll SHA-256=ce4980f1b44f0fab06c65b830bc031793cd260f59b5aee1dfb392651efdabec3
- PASS: installed_tunnel_helper_matches_official_pin — installed host helper matches packaged official release digest and includes license
- PASS: editable_source_preserved — isolated Source/keep.txt unchanged
- PASS: previous_build_retained — previous placeholder Build retained in same-volume backup
- PASS: public_logs_no_errors — installer SUCCESS and no exception, failure, or allocation-error markers in installer/native logs
- PASS: public_native_ui_images_present — four actual native UI screenshots produced with nonblank pixels

Limits: the installer job is prepared by this verification script; this does not exercise the in-game update button or upgrade an older published release. Native checks and screenshots establish startup, UI capture, installed-byte consistency, and source preservation. They do not establish subjective fun, physical controller input, public internet multiplayer, or human visual acceptance.

Evidence: public-verification.json, release.json, manifest.json, job/update-result.log, native/ui.json, native/runtime.log, and native PNG screenshots.

## Live in-game update check

The packaged Windows game passed all 11 live updater assertions against the published release, showing UP TO DATE, latest 0.2.0. The current public menu, Keybinds and update status screenshots were visually inspected.

The separate Build45 gameplay checks cover the actual public tunnel route with host/client game processes on one PC. Separate-PC environments, physical controls and human play feel remain hands-on checks. The full in-game download/install click path and upgrading an older published release are not claimed by the installer test.
