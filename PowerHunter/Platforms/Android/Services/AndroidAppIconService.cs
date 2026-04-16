using System.Collections.Concurrent;
using System.IO;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;

namespace PowerHunter.Platforms.Android.Services;

/// <summary>
/// Resolves installed Android application icons by package name and caches them
/// as MAUI image sources for reuse in the UI.
/// </summary>
public sealed class AndroidAppIconService : IAppIconService
{
    private readonly global::Android.Content.Context _context;
    private readonly LauncherApps? _launcherApps;
    private readonly PackageManager? _packageManager;
    private readonly ConcurrentDictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lazy<ImageSource?> _defaultIcon;
    private readonly int _iconSizePx;
    private readonly int _densityDpi;

    public AndroidAppIconService()
    {
        _context = global::Android.App.Application.Context;
        _packageManager = _context.PackageManager;
        var density = _context.Resources?.DisplayMetrics?.Density ?? 1f;
        _densityDpi = (int)(_context.Resources?.DisplayMetrics?.DensityDpi
            ?? global::Android.Util.DisplayMetricsDensity.Default);
        _iconSizePx = Math.Max((int)(48 * density), 96);
        _launcherApps = Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop
            ? _context.GetSystemService(Context.LauncherAppsService) as LauncherApps
            : null;
        _defaultIcon = new Lazy<ImageSource?>(CreateDefaultIconSource);
    }

    public ImageSource? GetIcon(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId) || _packageManager is null)
            return null;

        return _cache.GetOrAdd(appId, CreateIconSource);
    }

    public ImageSource? GetDefaultIcon() => _defaultIcon.Value;

    private ImageSource? CreateIconSource(string appId)
    {
        try
        {
            var drawable = ResolveIconDrawable(appId);
            if (drawable is null)
                return null;

            var pngBytes = ConvertDrawableToPng(drawable);
            if (pngBytes.Length == 0)
                return null;

            return ImageSource.FromStream(() => new MemoryStream(pngBytes));
        }
        catch (PackageManager.NameNotFoundException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private ImageSource? CreateDefaultIconSource()
    {
        try
        {
            var drawable = _context.GetDrawable(global::Android.Resource.Drawable.SymDefAppIcon);
            if (drawable is null)
                return null;

            var pngBytes = ConvertDrawableToPng(drawable);
            return pngBytes.Length == 0
                ? null
                : ImageSource.FromStream(() => new MemoryStream(pngBytes));
        }
        catch
        {
            return null;
        }
    }

    private Drawable? ResolveIconDrawable(string appId)
    {
        if (_packageManager is null)
            return null;

        var declaredIcon = TryLoadDeclaredIconResource(appId);
        if (declaredIcon is not null)
            return declaredIcon;

        var applicationIcon = TryGetApplicationIcon(appId);
        if (applicationIcon is not null)
            return applicationIcon;

        var launchIntentIcon = TryGetLaunchIntentIcon(appId);
        if (launchIntentIcon is not null)
            return launchIntentIcon;

        var launcherIcon = TryGetLauncherIcon(appId);
        if (launcherIcon is not null)
            return launcherIcon;

        return TryGetLauncherAppsIcon(appId);
    }

    private Drawable? TryGetLauncherAppsIcon(string appId)
    {
        if (_launcherApps is null || Build.VERSION.SdkInt < BuildVersionCodes.Lollipop)
            return null;

        try
        {
            var activities = _launcherApps.GetActivityList(appId, global::Android.OS.Process.MyUserHandle());
            if (activities is null || activities.Count == 0)
                return null;

            foreach (var activity in activities)
            {
                var icon = TryGetNonDefaultIcon(() => activity?.GetIcon(_densityDpi));
                if (icon is not null)
                    return icon;
            }
        }
        catch
        {
            // Continue through the rest of the resolution chain.
        }

        return null;
    }

    private Drawable? TryGetLaunchIntentIcon(string appId)
    {
        if (_packageManager is null)
            return null;

        try
        {
            var launchIntent = _packageManager.GetLaunchIntentForPackage(appId)
                ?? _packageManager.GetLeanbackLaunchIntentForPackage(appId);
            if (launchIntent is null)
                return null;

            var resolveInfo = _packageManager.ResolveActivity(launchIntent, 0);
            var resolvedIcon = TryGetNonDefaultIcon(() => resolveInfo?.LoadIcon(_packageManager));
            if (resolvedIcon is not null)
                return resolvedIcon;

            return TryLoadResolveInfoIcon(resolveInfo, appId);
        }
        catch
        {
            return null;
        }
    }

    private Drawable? TryGetLauncherIcon(string appId)
    {
        if (_packageManager is null)
            return null;

        try
        {
            var launcherIntent = new Intent(Intent.ActionMain);
            launcherIntent.AddCategory(Intent.CategoryLauncher);
            launcherIntent.SetPackage(appId);

            var matches = _packageManager.QueryIntentActivities(launcherIntent, 0);
            if (matches is null || matches.Count == 0)
                return null;

            foreach (var match in matches)
            {
                var icon = TryGetNonDefaultIcon(() => match?.LoadIcon(_packageManager));
                if (icon is not null)
                    return icon;

                var activityIcon = TryGetNonDefaultIcon(() => match?.ActivityInfo?.LoadIcon(_packageManager));
                if (activityIcon is not null)
                    return activityIcon;

                var declaredIcon = TryLoadResolveInfoIcon(match, appId);
                if (declaredIcon is not null)
                    return declaredIcon;
            }
        }
        catch
        {
            // Fall back to application icon lookup below.
        }

        return null;
    }

    private ApplicationInfo? TryGetApplicationInfo(string appId)
    {
        try
        {
            return _packageManager?.GetApplicationInfo(appId, 0);
        }
        catch (PackageManager.NameNotFoundException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private Drawable? TryLoadDeclaredIconResource(string appId)
    {
        var appInfo = TryGetApplicationInfo(appId);
        if (appInfo is null)
            return null;

        if (appInfo.Icon != 0)
        {
            var appIcon = TryLoadResourceIcon(appId, appInfo.Icon);
            if (appIcon is not null)
                return appIcon;
        }

        return null;
    }

    private Drawable? TryLoadResolveInfoIcon(ResolveInfo? resolveInfo, string appId)
    {
        if (resolveInfo is null)
            return null;

        var activityInfo = resolveInfo.ActivityInfo;
        if (activityInfo is not null)
        {
            if (activityInfo.Icon != 0)
            {
                var icon = TryLoadResourceIcon(activityInfo.PackageName ?? appId, activityInfo.Icon);
                if (icon is not null)
                    return icon;
            }
        }

        return null;
    }

    private Drawable? TryLoadResourceIcon(string packageName, int resourceId)
    {
        if (_packageManager is null || string.IsNullOrWhiteSpace(packageName) || resourceId == 0)
            return null;

        try
        {
            var resources = _packageManager.GetResourcesForApplication(packageName);
            var drawable = Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop
                ? resources.GetDrawableForDensity(resourceId, _densityDpi, _context.Theme)
                : resources.GetDrawable(resourceId);

            return IsDefaultApplicationIcon(drawable) ? null : drawable;
        }
        catch (PackageManager.NameNotFoundException)
        {
            return null;
        }
        catch (Resources.NotFoundException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private Drawable? TryGetApplicationIcon(string appId)
    {
        if (_packageManager is null)
            return null;

        var appInfo = TryGetApplicationInfo(appId);
        if (appInfo is not null)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var unbadgedIcon = TryGetNonDefaultIcon(() => appInfo.LoadUnbadgedIcon(_packageManager));
                if (unbadgedIcon is not null)
                    return unbadgedIcon;
            }

            var loadedIcon = TryGetNonDefaultIcon(() => appInfo.LoadIcon(_packageManager));
            if (loadedIcon is not null)
                return loadedIcon;
        }

        return TryGetNonDefaultIcon(() => _packageManager.GetApplicationIcon(appId));
    }

    private Drawable? TryGetNonDefaultIcon(Func<Drawable?> iconFactory)
    {
        try
        {
            var drawable = iconFactory();
            return IsDefaultApplicationIcon(drawable) ? null : drawable;
        }
        catch
        {
            return null;
        }
    }

    private bool IsDefaultApplicationIcon(Drawable? drawable)
    {
        if (drawable is null)
            return true;

        return Build.VERSION.SdkInt >= BuildVersionCodes.R
            && _packageManager?.IsDefaultApplicationIcon(drawable) == true;
    }

    private byte[] ConvertDrawableToPng(Drawable drawable)
    {
        var width = drawable.IntrinsicWidth > 1 ? drawable.IntrinsicWidth : _iconSizePx;
        var height = drawable.IntrinsicHeight > 1 ? drawable.IntrinsicHeight : _iconSizePx;
        var size = Math.Max(Math.Max(width, height), _iconSizePx);

        using var bitmap = Bitmap.CreateBitmap(size, size, Bitmap.Config.Argb8888!);
        using var canvas = new Canvas(bitmap);

        if (drawable is BitmapDrawable bitmapDrawable && bitmapDrawable.Bitmap is not null)
        {
            DrawBitmap(canvas, bitmapDrawable.Bitmap, size);
        }
        else
        {
            DrawDrawable(canvas, drawable, width, height, size);
        }

        return EncodeBitmap(bitmap);
    }

    private static void DrawDrawable(Canvas canvas, Drawable drawable, int sourceWidth, int sourceHeight, int targetSize)
    {
        var previousLeft = drawable.Bounds.Left;
        var previousTop = drawable.Bounds.Top;
        var previousRight = drawable.Bounds.Right;
        var previousBottom = drawable.Bounds.Bottom;

        var destination = CalculateDestinationRect(sourceWidth, sourceHeight, targetSize);
        drawable.SetBounds(destination.Left, destination.Top, destination.Right, destination.Bottom);
        drawable.Draw(canvas);
        drawable.SetBounds(previousLeft, previousTop, previousRight, previousBottom);
    }

    private static void DrawBitmap(Canvas canvas, Bitmap bitmap, int targetSize)
    {
        Bitmap? copiedBitmap = null;
        var renderBitmap = bitmap;

        if (bitmap.GetConfig() == Bitmap.Config.Hardware)
        {
            copiedBitmap = bitmap.Copy(Bitmap.Config.Argb8888!, false);
            if (copiedBitmap is not null)
                renderBitmap = copiedBitmap;
        }

        try
        {
            using var paint = new global::Android.Graphics.Paint
            {
                AntiAlias = true,
                Dither = true,
                FilterBitmap = true,
            };

            var sourceRect = new global::Android.Graphics.Rect(0, 0, renderBitmap.Width, renderBitmap.Height);
            var destinationRect = CalculateDestinationRect(renderBitmap.Width, renderBitmap.Height, targetSize);
            canvas.DrawBitmap(renderBitmap, sourceRect, destinationRect, paint);
        }
        finally
        {
            copiedBitmap?.Dispose();
        }
    }

    private static global::Android.Graphics.Rect CalculateDestinationRect(int sourceWidth, int sourceHeight, int targetSize)
    {
        var safeWidth = Math.Max(sourceWidth, 1);
        var safeHeight = Math.Max(sourceHeight, 1);
        var scale = Math.Min((float)targetSize / safeWidth, (float)targetSize / safeHeight);
        var drawWidth = Math.Max((int)Math.Round(safeWidth * scale), 1);
        var drawHeight = Math.Max((int)Math.Round(safeHeight * scale), 1);
        var left = (targetSize - drawWidth) / 2;
        var top = (targetSize - drawHeight) / 2;

        return new global::Android.Graphics.Rect(left, top, left + drawWidth, top + drawHeight);
    }

    private static byte[] EncodeBitmap(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        if (!bitmap.Compress(Bitmap.CompressFormat.Png!, 100, stream))
            return [];

        return stream.ToArray();
    }
}
