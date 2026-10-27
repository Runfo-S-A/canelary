using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Core.Services;

using YamlDotNet.Serialization;

namespace PreviewBinaryKits;

public class BinaryKits(string content) : IPreview
{
    private const string MetadataStart = "^FX Start Metadata#BinaryKits";
    private const string LegacyMetadataStart = "^FX Start Metadata#Labelary";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };
    private StringBuilder _error = new();
    private int? _productId;

    public string Content { get; private set; } = content;
    public string Error => _error.ToString();

    public IPreview LoadVariables<T>(int? id = null, T? extraData = null) where T : class
    {
        Content = BackendServiceProvider.Backend.LoadVariables(Content, id ?? _productId ?? 0, ref _error, extraData);
        return this;
    }

    public IPreview LoadVariables(int? id = null) => LoadVariables<object>(id);

    public bool HasMetadata() => Content.Contains(MetadataStart) || Content.Contains(LegacyMetadataStart);

    public IPreview ParseMetadata()
    {
        if (!HasMetadata())
        {
            return this;
        }

        var marker = Content.Contains(MetadataStart) ? MetadataStart : LegacyMetadataStart;
        var start = Content.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = Content.IndexOf("^FX End Metadata", start, StringComparison.Ordinal);
        try
        {
            if (end < start)
            {
                throw new FormatException("Falta ^FX End Metadata.");
            }

            // Language font substitutions are specific to Labelary's renderer.
            var metadata = new DeserializerBuilder().IgnoreUnmatchedProperties().Build()
                .Deserialize<Metadata>(Content[start..end].Trim().Replace("^FX ", ""));
            _productId = metadata?.ProductId;
        }
        catch (Exception ex)
        {
            _error.AppendLine($"Error al parsear la metadata: {ex.Message}");
        }
        return this;
    }

    public async Task<List<byte[]?>?> Build(string dpi, string size)
    {
        var result = await Render(Content, dpi, size);
        foreach (var command in result.NonSupportedCommands ?? [])
        {
            _error.AppendLine($"BinaryKits: comando no soportado: {command}");
        }

        return result.Labels?.Select(label => (byte[]?)Convert.FromBase64String(
            label.ImageBase64 ?? throw new JsonException("BinaryKits devolvió una etiqueta sin imagen."))).ToList();
    }

    public Task<string[]?> Linting(string codigo, string dpi, string size)
    {
        // The API reports unsupported commands without the positions required by the editor's linter.
        return Task.FromResult<string[]?>(null);
    }

    private static async Task<RenderResponse> Render(string zpl, string dpi, string size)
    {
        if (!Uri.TryCreate(SettingsService.Instance.BinaryKitsUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("BinaryKitsUrl debe ser una URL HTTP o HTTPS absoluta.");
        }

        var dimensions = size.Split('x');
        if (!int.TryParse(dpi, NumberStyles.Integer, CultureInfo.InvariantCulture, out var density) || density <= 0
            || dimensions.Length != 2
            || !double.TryParse(dimensions[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var width)
            || !double.TryParse(dimensions[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var height)
            || !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
        {
            throw new ArgumentException("La densidad y las dimensiones de la etiqueta deben ser positivas.");
        }

        using var response = await Client.PostAsJsonAsync(new Uri(baseUri, "api/v1/Viewer"), new
        {
            zplData = zpl,
            labelWidth = width * 25.4,
            labelHeight = height * 25.4,
            printDensityDpmm = density,
            type = "image"
        });
        if (!response.IsSuccessStatusCode)
        {
            // La primera línea del cuerpo trae el motivo (ej. el elemento ZPL inválido); el resto es el stack trace del servidor.
            var body = await response.Content.ReadAsStringAsync();
            var reason = body.Split('\n', 2)[0].Trim();
            throw new HttpRequestException(
                $"BinaryKits respondió {(int)response.StatusCode} ({response.ReasonPhrase}){(reason.Length > 0 ? $": {reason}" : ".")}",
                null, response.StatusCode);
        }
        return await response.Content.ReadFromJsonAsync<RenderResponse>()
            ?? throw new JsonException("BinaryKits devolvió una respuesta vacía.");
    }

    private sealed class Metadata
    {
        public int? ProductId { get; set; }
    }

    private sealed class RenderResponse
    {
        public string[]? NonSupportedCommands { get; set; }
        public RenderLabel[]? Labels { get; set; }
    }

    private sealed class RenderLabel
    {
        public string? ImageBase64 { get; set; }
    }
}
