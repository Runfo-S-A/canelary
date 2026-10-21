using System.IO;

using Core.Services.SettingsModel;

using YamlDotNet.Serialization;

namespace Core.Services;

/// <summary>
/// Singleton con los ajustes globales del sistema.
/// </summary>
public class SettingsService
{
    [YamlMember(Description = " - Cadena de conexión con la base de datos SQL Server")]
    public required string SqlConnection { get; set; }

    [YamlMember(Description = " - Tema del editor, posibles modos: Dark, Light")]
    public Theme Theme { get; set; } = Theme.Dark;

    [YamlMember(Description = " - Region a usar por el programa, para darle formato a las fechas y numeros")]
    public string Culture { get; set; } = "es-MX";

    [YamlMember(Description = " - Extensión de los archivos de etiqueta")]
    public List<string> Extension { get; set; } = ["e01", "e02"];

    [YamlMember(Description = " - Lista de directorios virtuales, con los cuales se generera la jerarquía de etiquetas")]
    public required List<FilterDirectory> VirtualDirectories { get; set; }

    [YamlMember(Description = " - Servicio responsable de generar una previsualización digital de la etiqueta seleccionada")]
    public string Preview { get; set; } = "Labelary";

    [YamlMember(Description = " - URL base de la API de BinaryKits")]
    public string BinaryKitsUrl { get; set; } = "http://localhost:8080/";

    [YamlMember(Description = " - Servicio encargado de proporcionar los datos necesarios para completar las variables encontradas en las etiquetas con datos reales")]
    public string Backend { get; set; } = "Twins";

    [YamlMember(Description = " - Servicio que administra el sistema de control de versionado a utilizar")]
    public string Version { get; set; } = "Git";

    [YamlMember(Description = " - Lista de modulos disponibles en el sistema")]
    public required List<Module> Modules { get; set; }

    [YamlMember(Description = " - UpdateInfo URL")]
    public required string UpdateUrl { get; set; }

    private static readonly Lazy<SettingsService> Lazy =
        new(() =>
        {
            var deserializer = new DeserializerBuilder().Build();
            return deserializer.Deserialize<SettingsService>(File.ReadAllText("Settings.yaml"));
        });

    public static SettingsService Instance => Lazy.Value;

    public static void Save()
    {
        var serializer = new SerializerBuilder().Build();
        var yaml = serializer.Serialize(Instance);
        File.WriteAllText("Settings.yaml", $"# Canelary archivo de configuración{Environment.NewLine}{yaml}");
    }
}
