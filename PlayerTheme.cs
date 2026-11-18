using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MeuPlayer;

internal record PlayerTheme(string Name, uint Surface, uint Border, uint Text, uint Detail, uint Accent)
{
    private static readonly PlayerTheme Dark = new("Escuro", 0xF01B222C, 0x665C6C80, 0xFFF1F5FA, 0xFFC8D6E6, 0xFF7AB1E8);
    internal static readonly PlayerTheme Adaptive = Dark with { Name = "Adaptativo" };

    internal static readonly PlayerTheme[] All =
    [
        new("Padrão (transparente)", 0x12000000, 0x00FFFFFF, 0xFFFFFFFF, 0xFFD8E0EA, 0xFF8BB8E8),
        Dark,
        new("Claro", 0xFFF2F5F9, 0xFFB5C2D0, 0xFF132033, 0xFF3A4B62, 0xFF3268A8),
        new("Neon", 0xFA101122, 0xFF7056F6, 0xFFEDFFFC, 0xFF80F9E1, 0xFF9A83FF),
        new("Pôr do sol", 0xFA371D27, 0xFFB25F50, 0xFFFFF3E8, 0xFFFFBB98, 0xFFFF925C),
        new("Oceano", 0xFA0D293D, 0xFF317196, 0xFFE8FAFF, 0xFFAFDDF2, 0xFF6DCCFF),
        new("Floresta", 0xFA172D22, 0xFF547B51, 0xFFF0F9E9, 0xFFBFDEB4, 0xFF83CC7A),
        new("Púrpura", 0xFA2D1F40, 0xFF8165A7, 0xFFF9EFFF, 0xFFE0C8F7, 0xFFC195FD),
        new("Rubi", 0xFA3B1927, 0xFFB35572, 0xFFFFF1F6, 0xFFFFC2D2, 0xFFF27F9E),
        new("Alto contraste", 0xFF000000, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFF00),
        Adaptive
    ];

    internal static PlayerTheme FromArtwork(BitmapSource? artwork)
    {
        if (artwork is null || artwork.PixelWidth == 0 || artwork.PixelHeight == 0) return Adaptive;

        const int sampleSize = 32;
        double scale = Math.Min(1d, (double)sampleSize / Math.Max(artwork.PixelWidth, artwork.PixelHeight));
        BitmapSource sample = scale < 1 ? new TransformedBitmap(artwork, new ScaleTransform(scale, scale)) : artwork;
        if (sample.Format != PixelFormats.Bgra32)
            sample = new FormatConvertedBitmap(sample, PixelFormats.Bgra32, null, 0);

        int stride = sample.PixelWidth * 4;
        var pixels = new byte[stride * sample.PixelHeight];
        sample.CopyPixels(pixels, stride, 0);
        var groups = new (double Weight, double Red, double Green, double Blue)[512];
        int best = 0;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            int blue = pixels[i], green = pixels[i + 1], red = pixels[i + 2], alpha = pixels[i + 3];
            if (alpha < 96) continue;

            int highest = Math.Max(red, Math.Max(green, blue));
            int lowest = Math.Min(red, Math.Min(green, blue));
            double chroma = (highest - lowest) / 255d;
            double weight = (0.12 + chroma * 1.8) * (alpha / 255d);
            int index = ((red >> 5) << 6) | ((green >> 5) << 3) | (blue >> 5);
            ref var group = ref groups[index];
            group.Weight += weight;
            group.Red += red * weight;
            group.Green += green * weight;
            group.Blue += blue * weight;
            if (group.Weight > groups[best].Weight) best = index;
        }

        var main = groups[best];
        if (main.Weight == 0) return Adaptive;
        uint hue = Pack(main.Red / main.Weight, main.Green / main.Weight, main.Blue / main.Weight);
        return Adaptive with
        {
            Surface = Mix(0xFF111318, hue, 0.24),
            Border = Mix(0xFF343943, hue, 0.45),
            Text = 0xFFF9FAFC,
            Detail = Mix(0xFFE5E8ED, hue, 0.18),
            Accent = Mix(hue, 0xFFFFFFFF, 0.42)
        };
    }

    private static uint Mix(uint first, uint second, double fraction) => Pack(
        BlendChannel(first, second, 16, fraction),
        BlendChannel(first, second, 8, fraction),
        BlendChannel(first, second, 0, fraction));

    private static double BlendChannel(uint first, uint second, int shift, double fraction) =>
        ((first >> shift) & 255) * (1 - fraction) + ((second >> shift) & 255) * fraction;

    private static uint Pack(double red, double green, double blue) =>
        0xFF000000 | ((uint)Math.Round(red) << 16) | ((uint)Math.Round(green) << 8) | (uint)Math.Round(blue);

    internal static SolidColorBrush Brush(uint argb)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        brush.Freeze();
        return brush;
    }
}
