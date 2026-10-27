using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LabelViewer;

/// <summary>
/// Versiones nativas de devops/rounded_image.py y devops/generate_image.py.
/// Usa solo BitmapSource (WIC), que funciona fuera de un hilo STA.
/// </summary>
public static class PostProcessors
{
    /// <summary>
    /// rounded_image.py: recorta a 4:3 desde arriba a la izquierda, escala a 8x6cm y vuelve transparentes las esquinas.
    /// </summary>
    public static byte[] Rounded(byte[] image, int radius = 95, double dpi = 300)
    {
        int width = (int)(8 / 2.54 * dpi), height = (int)(6 / 2.54 * dpi);
        var source = Decode(image);

        double ratio = (double)width / height;
        var crop = (double)source.PixelWidth / source.PixelHeight > ratio
            ? new Int32Rect(0, 0, (int)(source.PixelHeight * ratio), source.PixelHeight)
            : new Int32Rect(0, 0, source.PixelWidth, (int)(source.PixelWidth / ratio));

        // TransformedBitmap redondea el tamaño; se escala medio píxel de más y se recorta al tamaño exacto.
        var scaled = new TransformedBitmap(new CroppedBitmap(source, crop),
            new ScaleTransform((width + .5) / crop.Width, (height + .5) / crop.Height));
        var pixels = Pixels(new CroppedBitmap(scaled, new Int32Rect(0, 0, width, height)));

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double dx = Math.Max(radius - (x + .5), x + .5 - (width - radius));
                double dy = Math.Max(radius - (y + .5), y + .5 - (height - radius));
                if (dx > 0 && dy > 0 && dx * dx + dy * dy > radius * radius)
                {
                    pixels[(y * width + x) * 4 + 3] = 0;
                }
            }
        }

        return Encode(new PngBitmapEncoder(), BitmapSource.Create(width, height, dpi, dpi, PixelFormats.Bgra32, null, pixels, width * 4));
    }

    /// <summary>
    /// generate_image.py: pega la etiqueta (blanco = transparente) 30px a la derecha sobre el fondo y la guarda como JPEG.
    /// </summary>
    public static byte[] Composite(byte[] foreground, byte[] background, int quality = 75, double scale = 1.0)
    {
        const int shift = 30;
        var bgSource = Decode(background);
        var fgSource = Decode(foreground);
        int w = bgSource.PixelWidth, h = bgSource.PixelHeight, fw = fgSource.PixelWidth;
        var bg = Pixels(bgSource);
        var fg = Pixels(fgSource);

        for (int y = 0; y < Math.Min(h, fgSource.PixelHeight); y++)
        {
            for (int x = 0; x < Math.Min(fw, w - shift); x++)
            {
                int f = (y * fw + x) * 4, b = (y * w + x + shift) * 4;
                if (fg[f] == 255 && fg[f + 1] == 255 && fg[f + 2] == 255)
                {
                    continue;
                }

                double a = fg[f + 3] / 255.0;
                for (int c = 0; c < 3; c++)
                {
                    bg[b + c] = (byte)(bg[b + c] * (1 - a) + fg[f + c] * a);
                }
            }
        }

        BitmapSource result = BitmapSource.Create(w, h, bgSource.DpiX, bgSource.DpiY, PixelFormats.Bgra32, null, bg, w * 4);
        if (scale != 1.0)
        {
            result = new TransformedBitmap(result, new ScaleTransform(scale, scale));
        }

        return Encode(new JpegBitmapEncoder { QualityLevel = quality }, new FormatConvertedBitmap(result, PixelFormats.Bgr24, null, 0));
    }

    /// <summary>
    /// Umbraliza a blanco y negro puro (lo que imprime una térmica). Los grises del suavizado
    /// triplican el tamaño del PDF, porque el navegador reincrusta cada imagen como RGB sin pérdida.
    /// </summary>
    public static byte[] Monochrome(byte[] image)
    {
        var source = Decode(image);
        var pixels = Pixels(source);
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = pixels[i + 1] = pixels[i + 2] = (byte)(pixels[i] * 114 + pixels[i + 1] * 587 + pixels[i + 2] * 299 < 128_000 ? 0 : 255);
        }

        return Encode(new PngBitmapEncoder(), BitmapSource.Create(source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY, PixelFormats.Bgra32, null, pixels, source.PixelWidth * 4));
    }

    private static BitmapSource Decode(byte[] image)
    {
        using MemoryStream ms = new(image);
        return BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
    }

    private static byte[] Pixels(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        return pixels;
    }

    private static byte[] Encode(BitmapEncoder encoder, BitmapSource source)
    {
        encoder.Frames.Add(BitmapFrame.Create(source));
        using MemoryStream ms = new();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
