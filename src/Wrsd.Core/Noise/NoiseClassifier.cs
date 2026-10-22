namespace Wrsd.Core.Noise;

public static class NoiseClassifier
{
    private static readonly string[] NoiseSubstrings = new[]
    {
        @"Explorer\UserAssist",
        @"Explorer\RecentDocs",
        @"Explorer\RunMRU",
        @"Explorer\WordWheelQuery",
        @"Explorer\MountPoints2",
        @"Explorer\FeatureUsage",
        @"Explorer\SessionInfo",
        @"Diagnostics\DiagTrack",
        @"Session Manager\AppCompatCache",
        @"Services\bam",
        @"Services\dam",
        @"Windows NT\CurrentVersion\Prefetcher",
        @"\Tracing\",
        @"\DeliveryOptimization\",
        @"Direct3D\MostRecentApplication",
        @"Windows\CurrentVersion\CloudStore",
        @"Windows\CurrentVersion\Search\JumplistCache",
        @"Windows Error Reporting",
        @"IdentityCRL",
        @"PushNotifications",
        @"CapabilityAccessManager",
        @"ActivityHistory",
        @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
        @"Control\WMI\Security"
    };

    public static bool IsNoise(string keyPath)
    {
        if (string.IsNullOrWhiteSpace(keyPath)) return false;

        for (int i = 0; i < NoiseSubstrings.Length; i++)
        {
            if (keyPath.Contains(NoiseSubstrings[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
