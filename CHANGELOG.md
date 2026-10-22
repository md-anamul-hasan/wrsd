# Changelog

All notable changes to WRSD. Versioning follows [Semantic Versioning](https://semver.org/); the version lives in `Directory.Build.props`.

## [2.1.0] - 2026-10-22

First open-source release (MIT). Earlier builds were a paid product; this release removes licensing entirely, so every feature is free.

### Added
- Registry snapshots with five scan presets, a vault, and an O(N) diff engine with side-by-side value inspection.
- Noise suppression for Windows telemetry, search indexer and certificate churn.
- Threat Hunter with MITRE ATT&CK mapping and remediation `.reg` scripts.
- Undo / redo `.reg`, CSV, HTML and JSON exports for SIEM tools.
- `wrsd-cli` for scripted snapshots and diffs.
- Light and dark themes; self-contained installer.

### Removed
- Licence-key activation, the licence dialog, evaluation limits and the licence-server connection. Everything is unlocked.
