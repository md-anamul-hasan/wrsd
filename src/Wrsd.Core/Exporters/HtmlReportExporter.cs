using System.Net;
using System.Text;
using Wrsd.Core.Models;

namespace Wrsd.Core.Exporters;

public static class HtmlReportExporter
{
    public static string GenerateHtml(SnapshotDiffReport report, bool hideNoise = true)
    {
        var sb = new StringBuilder();
        var changes = report.Filter(hideNoise: hideNoise).ToList();

        sb.Append($$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>WRSD Registry Diff Report</title>
  <style>
    :root {
      --bg: #0F1115;
      --card-bg: #181B22;
      --border: #282C37;
      --text: #E6EDF3;
      --text-muted: #8B949E;
      --added: #10B981;
      --deleted: #F43F5E;
      --modified: #F59E0B;
      --risk: #A855F7;
    }
    body {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
      background: var(--bg);
      color: var(--text);
      margin: 0;
      padding: 30px;
    }
    .container { max-width: 1200px; margin: 0 auto; }
    header { margin-bottom: 24px; border-bottom: 1px solid var(--border); padding-bottom: 16px; }
    h1 { font-size: 24px; margin: 0 0 8px 0; font-weight: 600; display: flex; align-items: center; gap: 12px; }
    .badge { display: inline-block; padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 700; text-transform: uppercase; }
    .badge-added { background: rgba(16, 185, 129, 0.18); color: var(--added); border: 1px solid var(--added); }
    .badge-deleted { background: rgba(244, 63, 94, 0.18); color: var(--deleted); border: 1px solid var(--deleted); }
    .badge-modified { background: rgba(245, 158, 11, 0.18); color: var(--modified); border: 1px solid var(--modified); }
    .badge-risk { background: rgba(168, 85, 247, 0.2); color: var(--risk); border: 1px solid var(--risk); }
    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 14px; margin-bottom: 28px; }
    .stat-card { background: var(--card-bg); border: 1px solid var(--border); border-radius: 8px; padding: 14px; }
    .stat-val { font-size: 22px; font-weight: 700; margin-bottom: 4px; }
    .stat-lbl { font-size: 12px; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.5px; }
    .key-card { background: var(--card-bg); border: 1px solid var(--border); border-radius: 8px; margin-bottom: 14px; overflow: hidden; }
    .key-header { padding: 12px 16px; background: rgba(255, 255, 255, 0.02); border-bottom: 1px solid var(--border); display: flex; align-items: center; justify-content: space-between; font-family: monospace; font-size: 13px; }
    table { width: 100%; border-collapse: collapse; font-size: 12px; }
    th { text-align: left; padding: 8px 16px; color: var(--text-muted); border-bottom: 1px solid var(--border); font-weight: 600; }
    td { padding: 8px 16px; border-bottom: 1px solid rgba(255, 255, 255, 0.04); font-family: monospace; vertical-align: top; word-break: break-all; }
    tr:last-child td { border-bottom: none; }
  </style>
</head>
<body>
<div class="container">
  <header>
    <h1><span>WRSD</span> Registry Snapshot Diff Report</h1>
    <div style="font-size: 13px; color: var(--text-muted);">
      Baseline: <strong>{{WebUtility.HtmlEncode(report.Baseline.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"))}}</strong> | 
      Target: <strong>{{WebUtility.HtmlEncode(report.Target.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"))}}</strong> | 
      Generated: <strong>{{DateTime.Now:yyyy-MM-dd HH:mm:ss}}</strong>
    </div>
  </header>

  <div class="stats-grid">
    <div class="stat-card">
      <div class="stat-val" style="color: var(--added);">+{{report.AddedKeysCount}}</div>
      <div class="stat-lbl">Keys Added</div>
    </div>
    <div class="stat-card">
      <div class="stat-val" style="color: var(--deleted);">-{{report.DeletedKeysCount}}</div>
      <div class="stat-lbl">Keys Removed</div>
    </div>
    <div class="stat-card">
      <div class="stat-val" style="color: var(--modified);">~{{report.ModifiedKeysCount}}</div>
      <div class="stat-lbl">Keys Modified</div>
    </div>
    <div class="stat-card">
      <div class="stat-val" style="color: var(--text);">{{report.TotalValuesChanged}}</div>
      <div class="stat-lbl">Values Changed</div>
    </div>
    <div class="stat-card">
      <div class="stat-val" style="color: var(--risk);">{{report.PersistenceRisksCount}}</div>
      <div class="stat-lbl">Persistence Hooks</div>
    </div>
  </div>

  <div>
""");

        foreach (var k in changes)
        {
            string changeBadge = k.ChangeType switch
            {
                DiffChangeType.Added => "<span class=\"badge badge-added\">+ Added</span>",
                DiffChangeType.Deleted => "<span class=\"badge badge-deleted\">- Deleted</span>",
                _ => "<span class=\"badge badge-modified\">~ Modified</span>"
            };

            string riskBadge = k.IsPersistenceRisk
                ? $"<span class=\"badge badge-risk\">&#9888; {WebUtility.HtmlEncode(k.RiskCategory ?? "Persistence Risk")}</span>"
                : "";

            sb.Append($$"""
    <div class="key-card">
      <div class="key-header">
        <div><strong>{{WebUtility.HtmlEncode(k.KeyPath)}}</strong></div>
        <div style="display:flex; gap: 8px;">{{riskBadge}} {{changeBadge}}</div>
      </div>
""");

            if (k.ValueDiffs.Count > 0)
            {
                sb.Append("""
      <table>
        <thead>
          <tr>
            <th style="width: 25%;">Value Name</th>
            <th style="width: 10%;">Type</th>
            <th style="width: 10%;">Change</th>
            <th style="width: 27%;">Before</th>
            <th style="width: 28%;">After</th>
          </tr>
        </thead>
        <tbody>
""");

                foreach (var v in k.ValueDiffs)
                {
                    string vBadge = v.ChangeType switch
                    {
                        DiffChangeType.Added => "<span class=\"badge badge-added\">+ Add</span>",
                        DiffChangeType.Deleted => "<span class=\"badge badge-deleted\">- Del</span>",
                        _ => "<span class=\"badge badge-modified\">~ Mod</span>"
                    };

                    sb.Append($$"""
          <tr>
            <td><strong>{{WebUtility.HtmlEncode(string.IsNullOrEmpty(v.ValueName) ? "(Default)" : v.ValueName)}}</strong></td>
            <td>{{v.ValueTypeDisplay}}</td>
            <td>{{vBadge}}</td>
            <td style="color: var(--text-muted);">{{WebUtility.HtmlEncode(v.BeforeDisplay)}}</td>
            <td>{{WebUtility.HtmlEncode(v.AfterDisplay)}}</td>
          </tr>
""");
                }

                sb.Append("""
        </tbody>
      </table>
""");
            }

            sb.Append("""
    </div>
""");
        }

        sb.Append("""
  </div>
</div>
</body>
</html>
""");

        return sb.ToString();
    }
}
