## WRSD 2.1.0

![WRSD](https://raw.githubusercontent.com/md-anamul-hasan/wrsd/main/docs/media/screenshot-dark.png)

WRSD is now **free and open source (MIT)**. No licence key, no activation, no limits.

### Changed
- All features are unlocked; the licence dialog and evaluation mode are gone.
- The app makes no network connections at all now (the licence check was the only one); the command-line tool is included in the installer.

### Files
| File | Purpose |
|---|---|
| `WRSD-Setup-2.1.0.exe` | Installer: the app (self-contained) and `wrsd-cli.exe` (needs the shared .NET 10 runtime) |
| `SHA256SUMS.txt` | Checksum for the file above |

Verify a download: `Get-FileHash .\WRSD-Setup-2.1.0.exe -Algorithm SHA256` and compare with `SHA256SUMS.txt`.

Builds are not code-signed yet, so Windows SmartScreen may warn about an unknown publisher.

Full list of changes: [CHANGELOG.md](https://github.com/md-anamul-hasan/wrsd/blob/main/CHANGELOG.md).
