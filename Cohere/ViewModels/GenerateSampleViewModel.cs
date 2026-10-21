using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;

using Cohere.Models;

using Core.Database;
using Core.FileTree;
using Core.Helpers;
using Core.Services;
using Core.Services.BackendModel;

namespace Cohere.ViewModels;

public class GenerateSampleViewModel : BindableBase, IDialogAware
{
    public static string Title => "Generar Muestra";

    private const string CACHE_FILE = "after_command.cache";
    private const string TO_PNG = "To PNG";
    private const int MAX_HISTORY = 10;

    private readonly IDialogService _dialogService;
    private IFile _labelFile = null!;

    public ListCollectionView Printers { get; } = new(
        PrinterSettings.InstalledPrinters
            .Cast<string>()
            .Append(TO_PNG)
            .ToList()
        );

    public ObservableCollection<ProductoMuestra> ProductsList { get; set; } = [];

    public bool SelectAll
    {
        get;
        set
        {
            SetProperty(ref field, value);
            SelectedAll();
        }
    }

    public string Printer
    {
        get;
        set
        {
            SetProperty(ref field, value);
            RaisePropertyChanged(nameof(IsCmdVisible));
        }
    } = new PrinterSettings().PrinterName;

    public bool IsCmdVisible => Printer == TO_PNG;

    public ObservableCollection<string> AfterCommandHistory { get; } = [];

    public DateTime Fecha
    {
        get;
        set => SetProperty(ref field, value);
    } = DateTime.Now;

    public string AfterCommand
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public DelegateCommand CloseDialogCommand { get; private set; }

    public DialogCloseListener RequestClose { get; }

    public GenerateSampleViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;
        CloseDialogCommand = new(async () => await ClosingDialog());
        LoadCachedAfterCommandHistory();
        AfterCommand = AfterCommandHistory.FirstOrDefault() ?? string.Empty;
    }

    private void SelectedAll()
    {
        foreach (var item in ProductsList)
        {
            if (item.Enable)
            {
                item.Printable = SelectAll;
            }
        }
    }

    private async Task<int> PrintLabelsAsync(IEnumerable<ProductoMuestra> products, string printer, string outputPath)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        int savedImages = 0;

        ParallelOptions parallelOptions = new()
        {
            MaxDegreeOfParallelism = 4
        };

        await Parallel.ForEachAsync(products, parallelOptions, async (prod, cancellationToken) =>
        {
            if (prod.Printable)
            {
                var label = PreviewServiceProvider
                    .ProvideService(_labelFile.Read())
                    .ParseMetadata()
                    .LoadVariables(prod.Id, Fecha.ToString("yyyyMMdd"));
                var safeSenasa = new string([.. prod.Senasa.Select(c => invalidChars.Contains(c) ? '_' : c)]);
                if (printer == TO_PNG)
                {
                    if (!Directory.Exists(outputPath))
                    {
                        Directory.CreateDirectory(outputPath);
                    }

                    var images = await label.Build("12", "3.950x5.950"); // TODO!: Let the user set this parameters
                    Interlocked.Add(ref savedImages, GenerateImage(images, Path.Combine(outputPath, $"{safeSenasa} - {prod.Id}")));
                }
                else
                {
                    PrinterHelper.SendStringToPrinter(printer, label.Content, $"{_labelFile.Name} - {prod.Id}");
                }
            }
        });

        return savedImages;
    }

    private static int GenerateImage(List<byte[]?>? imageBytesList, string outputPath)
    {
        int savedImages = 0;
        if (imageBytesList == null || imageBytesList.Count == 0)
        {
            return savedImages;
        }

        try
        {
            int idx = 0;
            foreach (var bytes in imageBytesList)
            {
                idx++;

                if (bytes == null || bytes.Length == 0)
                {
                    continue;
                }

                using MemoryStream ms = new(bytes);
                using Bitmap bitmap = new(ms);
                bitmap.Save($"{outputPath}_{idx}.png", System.Drawing.Imaging.ImageFormat.Png);
                savedImages++;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"{ex.Message} --- {outputPath}");
        }
        return savedImages;
    }

    private async Task RunAfterCommandAsync(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "cmd.exe",
                Arguments = $"/C {command}",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("No se pudo iniciar el comando.");
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"El comando terminó con código {process.ExitCode}.");
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error executing after command: {ex.Message}");
            MessageBox.Show($"Error al ejecutar el comando: {ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            SaveCachedAfterCommand(command);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error saving command history: {ex.Message}");
            MessageBox.Show($"No se pudo guardar el historial de comandos: {ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LoadCachedAfterCommandHistory()
    {
        AfterCommandHistory.Clear();

        if (!File.Exists(CACHE_FILE))
        {
            return;
        }

        try
        {
            var history = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(CACHE_FILE));
            if (history is not null)
            {
                foreach (var command in history.Where(command => !string.IsNullOrWhiteSpace(command)).Distinct(StringComparer.Ordinal).Take(MAX_HISTORY))
                {
                    AfterCommandHistory.Add(command);
                }
            }
        }
        catch (JsonException)
        {
            // Old plain-text cache format or corrupt file: treat as no history.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceError($"Error loading command history: {ex.Message}");
            MessageBox.Show($"No se pudo leer el historial de comandos: {ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveCachedAfterCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        var history = AfterCommandHistory.Prepend(command)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal).Take(MAX_HISTORY).ToList();
        File.WriteAllText(CACHE_FILE, JsonSerializer.Serialize(history));

        AfterCommandHistory.Clear();
        foreach (var item in history)
        {
            AfterCommandHistory.Add(item);
        }

        // Collection notifications can clear the editable ComboBox's bound text.
        AfterCommand = command;
    }

    private async Task ClosingDialog()
    {
        string printer = Printer;
        string command = AfterCommand;
        string outputPath = Path.GetFullPath(@".\output");
        int savedImages = await PrintLabelsAsync(ProductsList, printer, outputPath);
        if (printer == TO_PNG)
        {
            await RunAfterCommandAsync(command);
            if (savedImages > 0 && MessageBox.Show("¿Desea abrir la carpeta de imágenes generadas?", Title,
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    if (!Directory.Exists(outputPath))
                    {
                        throw new DirectoryNotFoundException(outputPath);
                    }
                    using Process? process = Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"Error opening output folder: {ex.Message}");
                    MessageBox.Show($"No se pudo abrir la carpeta de imágenes: {ex.Message}", Title,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        RequestClose.Invoke();
    }

    public bool CanCloseDialog()
    {
        return true;
    }

    public void OnDialogClosed()
    { }

    public void OnDialogOpened(IDialogParameters parameters)
    {
        if (parameters.TryGetValue("File", out IFile? file) && file is not null)
        {
            _labelFile = file;

            //using var context = new IdeDbContext();
            //var labelName = Path.GetFileNameWithoutExtension(_labelFile.Name);
        }

        if (parameters.TryGetValue("Products", out IEnumerable<Product>? products) && products is not null)
        {
            var p = products.Select(p => new ProductoMuestra(p));
            foreach (var item in p)
            {
                ProductsList.Add(item);
            }
        }
    }
}
