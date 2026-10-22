# Quickstart — WRSD

**Time:** ~3 minutes · **You need:** Windows 10 or 11 (Admin rights recommended)

1. **Launch WRSD:**
   Run WRSD as Administrator (installed from the release). Nothing else needs to be installed.
   *Expected:* The Windows 11 Fluent dark-mode window opens with the 3-step Zen workflow.
2. **Step 1: Capture Baseline Snapshot:**
   Click "Take Snapshot" before installing software or applying system changes.
   *Expected:* Live progress scans `HKLM` and `HKCU` keys and saves snapshot to the Vault.
3. **Step 2: Capture Target Snapshot:**
   Install your software/driver, then return to WRSD and click "Take Second Snapshot".
4. **Step 3: Analyze the Diff:**
   - Review added, modified, and deleted keys.
   - Inspect side-by-side values (Before vs. After).
   - Check the **Threat Hunter** tab for MITRE ATT&CK persistence hooks (Run keys, services, debugger hijacks).
5. **Rollback System Changes:**
   Click "Generate Undo .reg" to export a 1-click script that restores altered registry values.

**Automation (CLI):** (needs the shared .NET 10 runtime: Settings & Rules ▸ Command-line tool ▸ Install .NET 10 runtime) `wrsd-cli.exe snapshot baseline.wrsd --preset quick`, then after the change `wrsd-cli.exe snapshot target.wrsd --preset quick`, then `wrsd-cli.exe diff baseline.wrsd target.wrsd --undo-reg rollback.reg --html report.html`. Run `wrsd-cli.exe` without arguments for all options.
