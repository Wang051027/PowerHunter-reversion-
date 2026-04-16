using PowerHunter.Models;

namespace PowerHunter.Services;

/// <summary>
/// Resolves installed apps into a compact user-facing taxonomy:
/// Game, Music &amp; Audio, Video, Tools, Social, Other.
///
/// Resolution order:
/// 1. Primary OS category hint from installer/manifest metadata
/// 2. Supplemental behavior signals from launcher registration and capabilities
/// 3. Known package catalog
/// 4. App label keyword fallback
/// </summary>
public static class AppCategoryResolver
{
    public const string Game = "Game";
    public const string MusicAudio = "Music & Audio";
    public const string Video = "Video";
    public const string Tools = "Tools";
    public const string Social = "Social";
    public const string Other = "Other";

    private static readonly string[] CategoryPriority =
    [
        Game,
        MusicAudio,
        Video,
        Tools,
        Social,
        Other,
    ];

    private static readonly Dictionary<string, double> CategoryWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        [Game] = 1.00,
        [MusicAudio] = 0.30,
        [Video] = 0.85,
        [Tools] = 0.35,
        [Social] = 0.48,
        [Other] = 0.35,
    };

    private static readonly Dictionary<string, string> CategoryColors = new(StringComparer.OrdinalIgnoreCase)
    {
        [Game] = "#F97316",
        [MusicAudio] = "#10B981",
        [Video] = "#EF4444",
        [Tools] = "#3B82F6",
        [Social] = "#EC4899",
        [Other] = "#94A3B8",
    };

    private static readonly Dictionary<string, string> LegacyCategoryAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Audio"] = MusicAudio,
        ["Image"] = Video,
        ["Productivity"] = Tools,
        ["Accessibility"] = Tools,
        ["Maps"] = Tools,
        ["Navigation"] = Tools,
        ["News"] = Other,
        ["Gaming"] = Game,
        ["Music"] = MusicAudio,
        ["Video"] = Video,
        ["Camera"] = Video,
        ["Browser"] = Tools,
        ["Productivity"] = Tools,
        ["Navigation"] = Tools,
        ["Utility"] = Tools,
        ["Work"] = Tools,
        ["Social"] = Social,
        ["Communication"] = Social,
        ["Study"] = Other,
        ["Entertainment"] = Other,
        ["System"] = Other,
        ["Unknown"] = Other,
        [Other] = Other,
    };

    private static readonly Dictionary<string, string> OriginalCategoryDisplayMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["game"] = Game,
        ["audio"] = "Audio",
        ["music"] = "Music",
        ["video"] = "Video",
        ["image"] = "Image",
        ["social"] = Social,
        ["productivity"] = "Productivity",
        ["accessibility"] = "Accessibility",
        ["maps"] = "Maps",
        ["navigation"] = "Navigation",
        ["news"] = "News",
        ["tools"] = Tools,
        ["undefined"] = Other,
    };

    private static readonly Dictionary<string, string> PrimaryHintMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["game"] = Game,
        ["audio"] = MusicAudio,
        ["music"] = MusicAudio,
        ["video"] = Video,
        ["image"] = Video,
        ["social"] = Social,
        ["productivity"] = Tools,
        ["news"] = Other,
        ["maps"] = Tools,
        ["navigation"] = Tools,
        ["accessibility"] = Tools,
        ["tools"] = Tools,
        ["undefined"] = Other,
    };

    private static readonly Dictionary<string, string> BehaviorTagMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["launcher-game"] = Game,
        ["launcher-music"] = MusicAudio,
        ["launcher-video"] = Video,
        ["launcher-social"] = Social,
        ["launcher-tools"] = Tools,
        ["media-playback-service"] = MusicAudio,
        ["immersive-video"] = Video,
        ["share-target"] = Social,
        ["messaging-capability"] = Social,
        ["tooling-capability"] = Tools,
    };

    private static readonly (string Prefix, string Category)[] KnownPackageCatalog =
    [
        // Game
        ("com.tencent.ig", Game),
        ("com.pubg", Game),
        ("com.supercell", Game),
        ("com.miHoYo", Game),
        ("com.hoyoverse", Game),
        ("com.garena", Game),
        ("com.activision", Game),
        ("com.epicgames", Game),
        ("com.riotgames", Game),
        ("com.ea.game", Game),
        ("com.gameloft", Game),
        ("com.king", Game),
        ("com.mojang", Game),
        ("com.innersloth", Game),
        ("com.netease.g", Game),
        ("com.nintendo", Game),

        // Music & Audio
        ("com.spotify", MusicAudio),
        ("com.apple.android.music", MusicAudio),
        ("com.google.android.apps.youtube.music", MusicAudio),
        ("com.netease.cloudmusic", MusicAudio),
        ("com.kugou", MusicAudio),
        ("com.tencent.qqmusic", MusicAudio),
        ("fm.xiami", MusicAudio),
        ("com.ximalaya", MusicAudio),

        // Video
        ("com.google.android.youtube", Video),
        ("com.google.android.apps.youtube.creator", Video),
        ("com.zhiliaoapp.musically", Video),
        ("com.ss.android.ugc.trill", Video),
        ("com.ss.android.ugc.aweme", Video),
        ("tv.twitch", Video),
        ("com.netflix", Video),
        ("com.disney", Video),
        ("com.amazon.avod", Video),
        ("com.bilibili", Video),
        ("com.kuaishou", Video),
        ("com.smile.gifmaker", Video),
        ("tv.danmaku.bili", Video),
        ("com.iqiyi", Video),
        ("com.youku", Video),
        ("com.mango", Video),

        // Social
        ("com.instagram", Social),
        ("com.facebook.katana", Social),
        ("com.facebook.orca", Social),
        ("com.twitter", Social),
        ("com.snapchat", Social),
        ("com.reddit", Social),
        ("com.linkedin", Social),
        ("com.pinterest", Social),
        ("com.sina.weibo", Social),
        ("com.tencent.mm", Social),
        ("com.tencent.mobileqq", Social),
        ("com.whatsapp", Social),
        ("org.telegram", Social),
        ("com.discord", Social),
        ("jp.naver.line", Social),
        ("com.xiaohongshu", Social),

        // Tools
        ("com.android.chrome", Tools),
        ("org.mozilla.firefox", Tools),
        ("com.brave.browser", Tools),
        ("com.opera", Tools),
        ("com.microsoft.emmx", Tools),
        ("com.tencent.mtt", Tools),
        ("com.google.android.apps.maps", Tools),
        ("com.autonavi", Tools),
        ("com.baidu.BaiduMap", Tools),
        ("com.google.android.gm", Tools),
        ("com.microsoft.outlook", Tools),
        ("com.microsoft.office", Tools),
        ("com.google.android.apps.docs", Tools),
        ("com.notion", Tools),
        ("com.todoist", Tools),
        ("com.alibaba.android.rimet", Tools),
        ("com.microsoft.teams", Tools),
        ("com.slack", Tools),
        ("com.android.settings", Tools),
        ("com.android.calculator", Tools),
        ("com.android.deskclock", Tools),
        ("com.android.vending", Tools),
    ];

    private static readonly (string Keyword, string Category)[] LabelKeywordCatalog =
    [
        ("game", Game),
        ("music", MusicAudio),
        ("audio", MusicAudio),
        ("radio", MusicAudio),
        ("podcast", MusicAudio),
        ("video", Video),
        ("player", Video),
        ("camera", Video),
        ("tube", Video),
        ("chat", Social),
        ("social", Social),
        ("message", Social),
        ("messenger", Social),
        ("browser", Tools),
        ("maps", Tools),
        ("mail", Tools),
        ("tool", Tools),
        ("file", Tools),
        ("note", Tools),
        ("calc", Tools),
    ];

    public static string Resolve(RawAppUsage rawUsage)
        => Resolve(rawUsage.PackageName, rawUsage.AppLabel, rawUsage.CategorySignal);

    public static string Resolve(string packageName)
        => Resolve(packageName, null, null);

    public static string Resolve(string packageName, string? appLabel, AppCategorySignal? categorySignal)
    {
        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (TryMapPrimaryHint(categorySignal?.PrimaryCategoryHint, out var primaryCategory))
        {
            AddScore(scores, primaryCategory, 100);
        }

        if (categorySignal?.BehaviorTags is not null)
        {
            foreach (var tag in categorySignal.BehaviorTags)
            {
                if (TryMapBehaviorTag(tag, out var behaviorCategory))
                {
                    AddScore(scores, behaviorCategory, 20);
                }
            }
        }

        if (TryResolveFromKnownPackage(packageName, out var knownCategory))
        {
            AddScore(scores, knownCategory, 12);
        }

        if (TryResolveFromLabel(appLabel, out var labelCategory))
        {
            AddScore(scores, labelCategory, 8);
        }

        if (scores.Count == 0)
            return Other;

        return scores
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => Array.IndexOf(CategoryPriority, entry.Key))
            .First()
            .Key;
    }

    public static string ResolveOriginalCategory(RawAppUsage rawUsage)
    {
        if (TryResolveOriginalCategory(rawUsage.CategorySignal?.PrimaryCategoryHint, out var originalCategory))
            return originalCategory;

        return Resolve(rawUsage);
    }

    public static string GetPreferredCategoryLabel(AppUsageRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.OriginalCategory))
            return record.OriginalCategory;

        return string.IsNullOrWhiteSpace(record.Category) ? Other : record.Category;
    }

    public static string Normalize(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return Other;

        if (LegacyCategoryAliases.TryGetValue(category, out var mappedCategory))
            return mappedCategory;

        return CategoryPriority.Contains(category, StringComparer.OrdinalIgnoreCase)
            ? CategoryPriority.First(value => string.Equals(value, category, StringComparison.OrdinalIgnoreCase))
            : Other;
    }

    public static double GetWeight(string category)
        => CategoryWeights.GetValueOrDefault(Normalize(category), CategoryWeights[Other]);

    public static double GetWeightForPackage(string packageName)
        => GetWeight(Resolve(packageName));

    public static string GetColor(string category)
        => CategoryColors.GetValueOrDefault(Normalize(category), CategoryColors[Other]);

    private static bool TryMapPrimaryHint(string? primaryHint, out string category)
    {
        if (!string.IsNullOrWhiteSpace(primaryHint) &&
            PrimaryHintMap.TryGetValue(primaryHint, out category!))
        {
            return true;
        }

        category = Other;
        return false;
    }

    private static bool TryMapBehaviorTag(string? behaviorTag, out string category)
    {
        if (!string.IsNullOrWhiteSpace(behaviorTag) &&
            BehaviorTagMap.TryGetValue(behaviorTag, out category!))
        {
            return true;
        }

        category = Other;
        return false;
    }

    private static bool TryResolveOriginalCategory(string? primaryHint, out string category)
    {
        if (!string.IsNullOrWhiteSpace(primaryHint) &&
            OriginalCategoryDisplayMap.TryGetValue(primaryHint, out category!))
        {
            return true;
        }

        category = Other;
        return false;
    }

    private static bool TryResolveFromKnownPackage(string packageName, out string category)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            category = Other;
            return false;
        }

        var match = KnownPackageCatalog
            .Where(entry => packageName.StartsWith(entry.Prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.Prefix.Length)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(match.Prefix))
        {
            category = Other;
            return false;
        }

        category = match.Category;
        return true;
    }

    private static bool TryResolveFromLabel(string? appLabel, out string category)
    {
        if (string.IsNullOrWhiteSpace(appLabel))
        {
            category = Other;
            return false;
        }

        var normalizedLabel = appLabel.Trim();
        foreach (var (keyword, mappedCategory) in LabelKeywordCatalog)
        {
            if (normalizedLabel.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                category = mappedCategory;
                return true;
            }
        }

        category = Other;
        return false;
    }

    private static void AddScore(IDictionary<string, int> scores, string category, int delta)
    {
        var normalizedCategory = Normalize(category);
        if (scores.TryGetValue(normalizedCategory, out var existing))
        {
            scores[normalizedCategory] = existing + delta;
            return;
        }

        scores[normalizedCategory] = delta;
    }
}
