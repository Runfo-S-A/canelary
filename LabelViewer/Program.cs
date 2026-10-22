using System.IO;

using BackendTwins.Models;

using Core.FileTree;
using Core.Models;
using Core.Services;

using LabelViewer;

using PreviewBinaryKits;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// SettingsService lee Settings.yaml relativo al directorio actual; se cambia después de Build para no mover el content root (wwwroot).
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

SettingsService.Instance.BinaryKitsUrl = app.Configuration["BinaryKitsUrl"] ?? SettingsService.Instance.BinaryKitsUrl;
BackendServiceProvider.Set(new Twins());
var etiquetasDir = app.Configuration["EtiquetasDir"] ?? throw new InvalidOperationException("Falta EtiquetasDir en appsettings.json");
var sizes = LabelSize.GetList(File.ReadAllText("SizeList.xml"));
var primariaSize = sizes.FirstOrDefault(s => s.Display.StartsWith("Primaria")).Value;
var israelSize = sizes.FirstOrDefault(s => s.Display.StartsWith("Israel")).Value;
var backgroundsDir = Path.Combine(AppContext.BaseDirectory, "backgrounds");

// shortcut: el índice se arma una vez (una consulta por etiqueta) y solo se refresca reiniciando; agregar un endpoint de recarga si las asociaciones cambian seguido.
// PublicationOnly: si la DB falla, el error no queda cacheado y el próximo request reintenta.
var index = new Lazy<List<ProductEntry>>(() => LabelIndex.Build(SettingsService.Instance.Extension
    .SelectMany(ext => Directory.GetFiles(etiquetasDir, $"*.{ext}"))
    .Select(path => (path, BackendServiceProvider.Backend.GetProducts(Path.GetFileNameWithoutExtension(path))))),
    LazyThreadSafetyMode.PublicationOnly);

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/KumbhSans.ttf", () => Results.File(Path.Combine(AppContext.BaseDirectory, "KumbhSans.ttf"), "font/ttf"));

app.MapGet("/api/options", async () => new
{
    Dpis = DpiConstants.All,
    Sizes = LabelSize.GetList(await File.ReadAllTextAsync("SizeList.xml")),
    Types = SettingsService.Instance.VirtualDirectories,
    Backgrounds = Directory.Exists(backgroundsDir) ? Directory.GetFiles(backgroundsDir, "*.png").Select(Path.GetFileName).Order().ToArray() : []
});

app.MapGet("/api/products", () => index.Value.Select(p => new
{
    p.Id,
    p.Code,
    p.Name,
    p.Senasa,
    Labels = p.Labels.Select(Path.GetFileName)
}));

app.MapGet("/api/products/{id}/preview", async (short id, string label, string dpi, string size, string? background, bool monochrome = false) =>
{
    // Solo etiquetas asociadas al producto: el nombre llega del cliente y no puede apuntar a otro archivo.
    var path = index.Value.Find(p => p.Id == id)?.Labels.Find(l => Path.GetFileName(l) == label);
    if (path is null)
    {
        return Results.NotFound();
    }

    // Israel se compone sobre un fondo; GetFileName impide salir de la carpeta de fondos.
    string? backgroundPath = size == israelSize && background is not null ? Path.Combine(backgroundsDir, Path.GetFileName(background)) : null;
    if (backgroundPath is not null && !File.Exists(backgroundPath))
    {
        return Results.NotFound();
    }

    IPreview? preview = null;
    List<byte[]?>? images = null;
    string? failure = null;
    try
    {
        // Mismo flujo que GenerateSampleViewModel.PrintLabelsAsync en Cohere.
        preview = new BinaryKits(new LabelFile(path).Read()).ParseMetadata().LoadVariables(id);
        images = await preview.Build(dpi, size);
        if (size == primariaSize)
        {
            images = [.. images?.Select(i => i is null ? null : PostProcessors.Rounded(i)) ?? []];
        }
        else if (backgroundPath is not null)
        {
            var backgroundImage = await File.ReadAllBytesAsync(backgroundPath);
            images = [.. images?.Select(i => i is null ? null : PostProcessors.Composite(i, backgroundImage)) ?? []];
        }

        // Para exportar: achica el PDF. Los compuestos no, el fondo es a color.
        if (monochrome && backgroundPath is null)
        {
            images = [.. images?.Select(i => i is null ? null : PostProcessors.Monochrome(i)) ?? []];
        }
    }
    catch (Exception ex)
    {
        failure = ex.Message;
    }

    // Las variables faltantes suelen explicar el fallo de BinaryKits, por eso se informan junto al error.
    var errors = (preview?.Error ?? "")
        .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
        .Where(line => !line.StartsWith("BinaryKits: comando no soportado"))
        .Prepend(failure)
        .OfType<string>();

    return Results.Ok(new
    {
        // Composite devuelve JPEG y el resto PNG: se envía el data URL con su tipo.
        Images = (images ?? []).OfType<byte[]>().Select(i => $"data:{(i[0] == 0xFF ? "image/jpeg" : "image/png")};base64,{Convert.ToBase64String(i)}"),
        Error = string.Join(Environment.NewLine, errors)
    });
});

app.Run();
