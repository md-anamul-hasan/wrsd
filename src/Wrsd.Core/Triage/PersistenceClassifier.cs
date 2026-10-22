namespace Wrsd.Core.Triage;

public static class PersistenceClassifier
{
    private static readonly (string Substring, string Category)[] PersistenceRules = new[]
    {
        (@"CurrentVersion\RunOnceEx", "RunOnceEx Autorun"),
        (@"CurrentVersion\RunOnce", "RunOnce Autorun"),
        (@"CurrentVersion\Run", "Standard Startup Autorun"),
        (@"Policies\Explorer\Run", "Group Policy Autorun"),
        (@"CurrentVersion\Winlogon", "Winlogon Shell / Userinit Hijack"),
        (@"Windows NT\CurrentVersion\Windows", "AppInit DLLs Hook"),
        (@"Image File Execution Options", "Debugger / IFEO Hijack"),
        (@"CurrentControlSet\Services", "Service or Driver Installation"),
        (@"Explorer\Shell Folders", "Shell Startup Folder Override"),
        (@"Explorer\User Shell Folders", "User Shell Startup Folder Override"),
        (@"Explorer\Browser Helper Objects", "Browser Helper Object (BHO)"),
        (@"ShellExecuteHooks", "Shell Execute Hook"),
        (@"SharedTaskScheduler", "Shared Task Scheduler Hook"),
        (@"Shell Extensions\Approved", "Approved Shell Extension"),
        (@"Control\Lsa", "LSA Security Package Hijack"),
        (@"CurrentVersion\SilentProcessExit", "Silent Process Exit Persistence"),
        (@"CurrentVersion\Accessibility", "Accessibility (StickyKeys) Hijack"),
        (@"Classes\Protocols\Filter", "URL Protocol Filter Hook"),
        (@"Classes\Protocols\Handler", "URL Protocol Handler Hook")
    };

    public static (bool IsRisk, string? Category) CheckPersistenceRisk(string keyPath)
    {
        if (string.IsNullOrWhiteSpace(keyPath)) return (false, null);

        for (int i = 0; i < PersistenceRules.Length; i++)
        {
            if (keyPath.Contains(PersistenceRules[i].Substring, StringComparison.OrdinalIgnoreCase))
            {
                return (true, PersistenceRules[i].Category);
            }
        }

        return (false, null);
    }
}
