using System.Buffers;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Windows.Storage;

namespace EasyUninstall.Services;

/// <summary>Source of an icon to extract: a file path with an optional icon index.</summary>
public sealed record IconSource(string Path, int IconIndex = 0);

/// <summary>
/// Extracts application icons to PNG files under a local cache directory and returns
/// cached file paths. Approach adapted from EasyLunch: SHA-256 keyed cache,
/// PrivateExtractIcons for high-resolution extraction with ExtractAssociatedIcon
/// fallback, then alpha-bounds normalization onto a transparent square canvas.
/// All failures return null; callers fall back to a placeholder.
/// </summary>
public static class IconExtractService
{
    private const int OutputSize = 64;
    private const int AlphaThreshold = 10;

    private static readonly string CacheDirectory = Path.Combine(
        ApplicationData.Current.LocalFolder.Path, "IconCache");

    /// <summary>
    /// Resolves the display icon for an app. Preferred sources first (e.g. DisplayIcon
    /// before the uninstall exe). Returns an absolute PNG path, or null.
    /// </summary>
    public static string? GetIconPath(params IconSource?[] sources)
    {
        foreach (var source in sources)
        {
            var path = GetIconPath(source);
            if (path is not null) return path;
        }
        return null;
    }

    public static string? GetIconPath(IconSource? source)
    {
        if (source is null || !File.Exists(source.Path)) return null;

        try
        {
            Directory.CreateDirectory(CacheDirectory);
            var lastWrite = File.GetLastWriteTimeUtc(source.Path).Ticks;
            var cacheName = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(
                    $"easyuninstall-icon-v1|{source.Path.ToLowerInvariant()}|{source.IconIndex}|{lastWrite}"))) + ".png";
            var cachePath = Path.Combine(CacheDirectory, cacheName);
            if (File.Exists(cachePath) && new FileInfo(cachePath).Length > 0)
            {
                return cachePath;
            }

            using var bitmap = TryLoadBitmapIconFile(source.Path) ?? TryExtractNativeIconBitmap(source);
            if (bitmap is null) return null;

            using var normalized = Normalize(bitmap);
            normalized.Save(cachePath, ImageFormat.Png);
            return cachePath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Displays a Store package logo; those are already image files when present.</summary>
    public static string? GetImageFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        var extension = Path.GetExtension(path);
        return extension is ".png" or ".jpg" or ".jpeg" or ".bmp" ? path : null;
    }

    private static Bitmap? TryLoadBitmapIconFile(string path)
    {
        var extension = Path.GetExtension(path);
        if (!new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            if (string.Equals(extension, ".ico", StringComparison.OrdinalIgnoreCase))
            {
                using var icon = new Icon(path);
                return icon.ToBitmap();
            }
            using var source = new Bitmap(path);
            return new Bitmap(source);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? TryExtractNativeIconBitmap(IconSource source)
    {
        var handles = new IntPtr[1];
        var ids = new uint[1];

        try
        {
            _ = PrivateExtractIcons(source.Path, source.IconIndex, OutputSize, OutputSize, handles, ids, 1, 0);
            if (handles[0] != IntPtr.Zero)
            {
                return RenderIconHandle(handles[0]);
            }
        }
        catch
        {
            // Fall through to ExtractAssociatedIcon.
        }
        finally
        {
            if (handles[0] != IntPtr.Zero)
            {
                DestroyIcon(handles[0]);
            }
        }

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(source.Path);
            return icon is null ? null : RenderIconHandle(icon.Handle);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? RenderIconHandle(IntPtr hIcon)
    {
        if (hIcon == IntPtr.Zero) return null;

        var bitmap = new Bitmap(OutputSize, OutputSize, PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent);
            var hdc = graphics.GetHdc();
            try
            {
                if (!DrawIconEx(hdc, 0, 0, hIcon, OutputSize, OutputSize, 0, IntPtr.Zero, DI_NORMAL))
                {
                    bitmap.Dispose();
                    return null;
                }
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            return null;
        }
    }

    /// <summary>Crops to the visible alpha bounds and centers the content on a transparent square.</summary>
    private static Bitmap Normalize(Bitmap source)
    {
        using var argb = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(argb))
        {
            g.Clear(Color.Transparent);
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(source, 0, 0, source.Width, source.Height);
        }

        var bounds = GetVisibleBounds(argb);
        var normalized = new Bitmap(OutputSize, OutputSize, PixelFormat.Format32bppArgb);
        if (bounds.Width <= 0 || bounds.Height <= 0) return normalized;

        var scale = Math.Min((double)OutputSize / bounds.Width, (double)OutputSize / bounds.Height);
        var width = Math.Max(1, (int)Math.Round(bounds.Width * scale));
        var height = Math.Max(1, (int)Math.Round(bounds.Height * scale));

        using (var g = Graphics.FromImage(normalized))
        {
            g.Clear(Color.Transparent);
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            var dest = new Rectangle((OutputSize - width) / 2, (OutputSize - height) / 2, width, height);
            g.DrawImage(argb, dest, bounds, GraphicsUnit.Pixel);
        }
        return normalized;
    }

    private static Rectangle GetVisibleBounds(Bitmap bitmap)
    {
        var left = bitmap.Width; var top = bitmap.Height;
        var right = -1; var bottom = -1;

        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var stride = data.Stride;
            var buffer = new byte[stride * bitmap.Height];
            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    if (buffer[y * stride + x * 4 + 3] <= AlphaThreshold) continue;
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return right < left ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    private const uint DI_NORMAL = 0x0003;

    [DllImport("User32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint PrivateExtractIcons(string szFileName, int nIconIndex, int cxIcon, int cyIcon, IntPtr[] phicon, uint[] piconid, uint nIcons, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyWidth, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
