# Public0.4.0 verification

The anonymous latest-release API returned v0.4.0. The actual published updater manifest matched the local release manifest exactly. Both public Windows and Source archives were downloaded and matched local SHA256 and byte counts. The Windows archive is 61,466,199 bytes with SHA256 `81a9ec142c58276b5149502a8971a42bfb6e4bdebbb4fc31deb3d2447befa6aa`.

The current native in-game release check passed 15 assertions, including the actual public0.4.0 manifest and UP TO DATE status. These checks follow the228 pre-publication assertions in VALIDATION.md. The frozen227 source/runtime file identities remained unchanged after publication.

The actual Desktop game was replaced from the matching archive;145 runtime/launcher/installer identities were checked and the generic Desktop shortcut points to that current game. A normal Desktop launch remained running. Public asset and native updater reports are in release-evidence/0.4.0-public-assets.json and0.4.0-public-updater.json.

The installer is unchanged. Prior0.3.1 isolated installer/restart evidence is historical; a full older-install button-driven upgrade, physical controllers, human competitive balance and separate-PC internet acceptance remain hands-on checks. The current copied-invite tunnel test used two native processes on one PC.
