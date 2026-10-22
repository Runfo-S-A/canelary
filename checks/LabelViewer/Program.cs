using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Core.Services.BackendModel;

using LabelViewer;

var a = new Product { Id = 1, Code = "A", Name = "Producto A" };
var b = new Product { Id = 2, Code = "B", Name = "Producto B" };

var index = LabelIndex.Build(
[
    (@"C:\et\CAJA_1.e01", [a, b]),
    (@"C:\et\PRIMARIA_1.e02", [new Product { Id = 1, Code = "A", Name = "Producto A" }]),
    (@"C:\et\SIN_PRODUCTOS.e01", []),
]);

Assert(index.Count == 2, "Each product appears once");
Assert(index[0].Id == 1 && index[0].Labels.SequenceEqual([@"C:\et\CAJA_1.e01", @"C:\et\PRIMARIA_1.e02"]), "Product in two labels keeps both");
Assert(index[1].Id == 2 && index[1].Labels.SequenceEqual([@"C:\et\CAJA_1.e01"]), "Product in one label");
Console.WriteLine("PASS: label -> product inversion groups by product id and keeps every label.");

// 4x6 a 12dpmm (1200x1800), gris; el hilo principal es MTA como los de ASP.NET.
var gray = Png(1200, 1800, 128, 255);
var rounded = Decode(PostProcessors.Rounded(gray));
var px = Bgra(rounded);
int Alpha(int x, int y) => px[(y * rounded.PixelWidth + x) * 4 + 3];
Assert(rounded.PixelWidth == 944 && rounded.PixelHeight == 708 && Math.Round(rounded.DpiX) == 300, "Rounded is 8x6cm at 300dpi");
Assert(Alpha(0, 0) == 0 && Alpha(943, 707) == 0 && Alpha(943, 0) == 0 && Alpha(0, 707) == 0, "Corners are transparent");
Assert(Alpha(472, 0) == 255 && Alpha(0, 354) == 255 && Alpha(472, 354) == 255, "Edges and center stay opaque");

var background = Png(200, 100, 0, 255);
var foreground = Png(200, 100, 255, 255); // blanco: debe desaparecer
var fgPixels = Bgra(Decode(foreground));
for (int y = 8; y < 24; y++)
{
    for (int x = 0; x < 16; x++)
    {
        fgPixels[(y * 200 + x) * 4 + 1] = 0; // bloque magenta 16x16; JPEG desdibuja píxeles sueltos
    }
}
var composite = Decode(PostProcessors.Composite(Encode(fgPixels, 200, 100), background, quality: 100));
var cp = Bgra(composite);
Assert(composite.PixelWidth == 200 && cp[(50 * 200 + 100) * 4] < 10, "White foreground is transparent");
Assert(cp[(16 * 200 + 38) * 4] > 200 && cp[(16 * 200 + 38) * 4 + 1] < 60, "Foreground is shifted 30px right");
Assert(cp[(16 * 200 + 8) * 4] < 30, "Unshifted position keeps background");
Assert(Decode(PostProcessors.Composite(foreground, background, scale: .5)).PixelWidth == 100, "Scale resizes output");
Console.WriteLine("PASS: rounded corners (8x6cm, 300dpi) and background composite (white transparent, 30px shift, scale).");

var grayPixels = Bgra(Decode(Png(4, 1, 0, 255)));
byte[] levels = [40, 127, 128, 220];
for (int i = 0; i < 4; i++)
{
    grayPixels[i * 4] = grayPixels[i * 4 + 1] = grayPixels[i * 4 + 2] = levels[i];
}
grayPixels[3 * 4 + 3] = 0; // píxel transparente (esquina redondeada)
var mono = Bgra(Decode(PostProcessors.Monochrome(Encode(grayPixels, 4, 1))));
Assert(mono[0] == 0 && mono[4] == 0 && mono[8] == 255 && mono[12] == 255, "Gray levels threshold to black/white at 128");
Assert(mono[0 + 1] == mono[0] && mono[0 + 2] == mono[0], "Output is neutral gray");
Assert(mono[3] == 255 && mono[15] == 0, "Alpha is preserved");
Console.WriteLine("PASS: monochrome thresholds to pure black/white and keeps alpha.");

static byte[] Png(int w, int h, byte value, byte alpha)
{
    var pixels = new byte[w * h * 4];
    for (int i = 0; i < pixels.Length; i += 4)
    {
        pixels[i] = pixels[i + 1] = pixels[i + 2] = value;
        pixels[i + 3] = alpha;
    }
    return Encode(pixels, w, h);
}

static byte[] Encode(byte[] pixels, int w, int h)
{
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4)));
    using var ms = new MemoryStream();
    encoder.Save(ms);
    return ms.ToArray();
}

static BitmapSource Decode(byte[] bytes) => BitmapDecoder.Create(new MemoryStream(bytes), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];

static byte[] Bgra(BitmapSource source)
{
    var c = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
    var pixels = new byte[c.PixelWidth * c.PixelHeight * 4];
    c.CopyPixels(pixels, c.PixelWidth * 4, 0);
    return pixels;
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception("FAIL: " + message);
    }
}
