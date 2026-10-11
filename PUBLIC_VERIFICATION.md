# Public release verification

Result: PASS.

The verification fetches the latest release and its assets anonymously from GitHub. It compares the actual public Windows ZIP with the public update manifest and the current local delivery ZIP, then uses the trusted bundled installer in an isolated installation and restarts the downloaded Windows game.

- PASS: public_latest_version — v0.3.0
- PASS: public_assets_ready — manifest and Windows ZIP from expected public repository
- PASS: public_manifest_matches_release — version, URL, asset size, and SHA-256 format verified
- PASS: public_archive_matches_manifest — anonymous download: 97453055 bytes, SHA-256 a114868d926b8fc6d8a089aedb1fa646e2d452462ea98b8616f50d4ae0ab9424
- PASS: public_archive_matches_local_delivery — actual public bytes match current local Windows ZIP
- PASS: public_source_asset_ready — editable source ZIP is present in the public release
- PASS: public_source_archive_matches_local_delivery — actual public source archive SHA256 matches the curated local source delivery
- PASS: public_source_archive_crc_valid — public source ZIP decompresses correctly and contains editable game/network sources
- PASS: trusted_installer_succeeded — exit=0; bundled helper SHA-256=5f7945aa02bf59bb8267ec1d9a10ee69302971ab19c3ac3026a42c0d161b4b87
- PASS: public_game_restarted_and_finished — installer-launched public Windows game wrote native UI report
- PASS: public_native_checks_pass — checks=2; issues=0; build=0.3.0
- PASS: installed_assembly_matches_public_archive — Assembly-CSharp.dll SHA-256=d0c6c5b77c023bd9c3025bf41112157db421f45a7568ce419068c754f1214a4d
- PASS: installed_tunnel_helper_matches_official_pin — installed host helper matches packaged official release digest and includes license
- PASS: editable_source_preserved — isolated Source/keep.txt unchanged
- PASS: previous_build_retained — previous placeholder Build retained in same-volume backup
- PASS: public_logs_no_errors — installer SUCCESS and no exception, failure, or allocation-error markers in installer/native logs
- PASS: public_native_ui_images_present — four actual native UI screenshots produced with nonblank pixels

Limits: the installer job is prepared by this verification script; this does not exercise the in-game update button or upgrade an older published release. Native checks and screenshots establish startup, UI capture, installed-byte consistency, and source preservation. They do not establish subjective fun, physical controller input, public internet multiplayer, or human visual acceptance.

Evidence: public-verification.json, release.json, manifest.json, job/update-result.log, native/ui.json, native/runtime.log, and native PNG screenshots.

The current native game's public update check passed 11 assertions. The published 0.2.0 game's updater discovered UPDATE 0.3.0 READY with restart required. These checks establish public discovery; the complete user-clicked older-install upgrade remains a hands-on check. Final current gameplay and networking reports passed 187 assertions, including an actual public tunnel route between two native processes on one PC.
