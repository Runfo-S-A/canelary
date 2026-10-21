using System.Buffers.Binary;
using System.Globalization;
using System.IO;

using Core.Services;

using PreviewBinaryKits;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static async Task Reject(Func<Task> action)
{
    try { await action(); }
    catch (ArgumentException) { return; }
    throw new Exception("Expected invalid settings to be rejected.");
}

var originalDirectory = Environment.CurrentDirectory;
var temporaryDirectory = Path.Combine(Path.GetTempPath(), "Canelary-BinaryKits-" + Guid.NewGuid());
Directory.CreateDirectory(temporaryDirectory);
try
{
    Environment.CurrentDirectory = temporaryDirectory;
    File.WriteAllText("Settings.yaml", "SqlConnection: ''\nVirtualDirectories: []\nModules: []\nUpdateUrl: ''\nPreview: BinaryKits\n");
    Check(SettingsService.Instance.BinaryKitsUrl == "http://localhost:8080/", "Default URL");
    SettingsService.Instance.BinaryKitsUrl = args.FirstOrDefault() ?? "http://localhost:8080/";
    new BinaryKitsModule().OnInitialized(null!);
    Check(PreviewServiceProvider.ProvideService("") is BinaryKits, "Module selection");

    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-AR");
    const string label = "^XA^FO10,10^A0N,20,20^FDTest^FS^XZ";
    var preview = PreviewServiceProvider.ProvideService(label + label);
    var images = await preview.Build("8", "2x1");
    Check(images?.Count == 2, "Expected two labels");
    foreach (var image in images!)
    {
        Check(image is { Length: > 24 } && image.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "PNG signature");
        Check(BinaryPrimitives.ReadInt32BigEndian(image!.AsSpan(16, 4)) == 406, "Width must convert inches to millimeters");
        Check(BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(20, 4)) == 203, "Height must convert inches to millimeters");
    }
    Check(await preview.Linting(label, "8", "2x1") is null, "Unsupported positional linting");
    await Reject(async () => { await preview.Build("0", "2x1"); });
    await Reject(async () => { await preview.Build("8", "NaNx1"); });
    await Reject(async () => { await preview.Build("8", "invalid"); });
    SettingsService.Instance.BinaryKitsUrl = "file:///invalid";
    await Reject(async () => { await preview.Build("8", "2x1"); });
    foreach (var engine in new[] { "BinaryKits", "Labelary" })
    {
        var metadata = new BinaryKits($"^FX Start Metadata#{engine}\n^FX ProductId: 42\n^FX End Metadata\n{label}");
        Check(metadata.HasMetadata(), "Metadata detection");
        metadata.ParseMetadata();
        Check(metadata.Error.Length == 0, "Metadata parsing");
    }
    var malformed = new BinaryKits("^FX Start Metadata#BinaryKits");
    malformed.ParseMetadata();
    Check(malformed.Error.Length > 0, "Malformed metadata must report an error");
    Console.WriteLine("BinaryKits checks passed: live rendering, dimensions, multiple labels, configuration, metadata, validation.");
}
finally
{
    Environment.CurrentDirectory = originalDirectory;
    Directory.Delete(temporaryDirectory, true);
}
