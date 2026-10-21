using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

using Cohere.ViewModels;

internal static class Program
{
    private const string Cache = "after_command.cache";

    [STAThread]
    private static void Main()
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        string originalDirectory = Environment.CurrentDirectory;
        string testDirectory = Path.Combine(Path.GetTempPath(), "Canelary-check-" + Guid.NewGuid());
        Directory.CreateDirectory(testDirectory);
        Environment.CurrentDirectory = testDirectory;
        try
        {
            CheckHistory();
            CheckImages();
            Console.WriteLine("PASS: history, editable ComboBox, reload, blank input, MRU cap, CMD success/failure, cache-write errors, and image save counts.");
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            Directory.Delete(testDirectory, true);
        }
    }

    private static void CheckHistory()
    {
        File.WriteAllText(Cache, JsonSerializer.Serialize(new string?[] { null, "", "  ", "echo first", "echo second", "echo first" }));
        var vm = new GenerateSampleViewModel(null!);
        Equal(vm.AfterCommandHistory, "echo first", "echo second");

        var combo = new ComboBox { IsEditable = true, DataContext = vm };
        combo.SetBinding(ComboBox.ItemsSourceProperty, new Binding("AfterCommandHistory"));
        combo.SetBinding(ComboBox.TextProperty, new Binding("AfterCommand") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        combo.ApplyTemplate();
        combo.SelectedItem = "echo second";
        Pump();
        Assert(vm.AfterCommand == "echo second", "Dropdown selection must populate input");
        Wait((Task)Invoke(vm, "RunAfterCommandAsync", vm.AfterCommand)!);
        Pump();
        Assert(vm.AfterCommand == "echo second" && combo.Text == "echo second", "Reordering must preserve command input");
        Equal(new GenerateSampleViewModel(null!).AfterCommandHistory, "echo second", "echo first");

        // Force the same synchronous text reset that a selected item removal can cause.
        vm.AfterCommandHistory.CollectionChanged += (_, _) => vm.AfterCommand = "";
        Save(vm, "echo new");
        Assert(vm.AfterCommand == "echo new", "Collection notifications must not replace the captured command");
        Equal(new GenerateSampleViewModel(null!).AfterCommandHistory, "echo new", "echo second", "echo first");
        string before = File.ReadAllText(Cache);
        vm.AfterCommand = "";
        Save(vm, "");
        Save(vm, " \t ");
        Wait((Task)Invoke(vm, "RunAfterCommandAsync", " \t ")!);
        Equal(new GenerateSampleViewModel(null!).AfterCommandHistory, "echo new", "echo second", "echo first");
        Assert(File.ReadAllText(Cache) == before, "Blank commands must not change history");

        for (int i = 0; i < 12; i++) Save(vm, $"echo {i}");
        Equal(new GenerateSampleViewModel(null!).AfterCommandHistory, Enumerable.Range(2, 10).Reverse().Select(i => $"echo {i}").ToArray());
        File.WriteAllText(Cache, "legacy plain text");
        Assert(new GenerateSampleViewModel(null!).AfterCommandHistory.Count == 0, "Legacy format remains ignored");
        File.WriteAllText(Cache, JsonSerializer.Serialize(Enumerable.Range(0, 12).Select(i => $"echo {i}")));
        Assert(new GenerateSampleViewModel(null!).AfterCommandHistory.Count == 10, "Load must cap history");

        var blocked = new GenerateSampleViewModel(null!);
        string previous = File.ReadAllText(Cache);
        using (var locked = new FileStream(Cache, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try
            {
                Save(blocked, "echo unwritable");
                throw new Exception("Expected cache write failure");
            }
            catch (TargetInvocationException ex) when (ex.InnerException is IOException) { }
        }
        Assert(File.ReadAllText(Cache) == previous, "Write failure must preserve retained history");

        var messages = new List<string>();
        var dismissErrors = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        dismissErrors.Tick += (_, _) =>
        {
            nint window = FindWindow("#32770", GenerateSampleViewModel.Title);
            if (window == 0) return;
            GetWindowThreadProcessId(window, out uint processId);
            if (processId != Environment.ProcessId) return;
            EnumChildWindows(window, (child, _) =>
            {
                var text = new StringBuilder(2048);
                GetWindowText(child, text, text.Capacity);
                messages.Add(text.ToString());
                return true;
            }, 0);
            SendMessage(window, 0x0111, 1, 0); // Acknowledge only this check process's error dialog.
        };
        dismissErrors.Start();
        try
        {
            Wait((Task)Invoke(blocked, "RunAfterCommandAsync", "exit /b 7")!);
            Assert(messages.Any(text => text.Contains("código 7")), "Nonzero exit must report a command error");
            Assert(File.ReadAllText(Cache) == previous, "Failed command must not promote history");
            messages.Clear();
            using var locked = new FileStream(Cache, FileMode.Open, FileAccess.Read, FileShare.Read);
            Wait((Task)Invoke(blocked, "RunAfterCommandAsync", "exit /b 0")!);
            Assert(messages.Any(text => text.Contains("guardar el historial")), "Cache write failure must have a separate message");
            Assert(File.ReadAllText(Cache) == previous, "Cache write failure must preserve persisted history");
        }
        finally { dismissErrors.Stop(); }
    }

    private static void CheckImages()
    {
        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
        int SaveImages(List<byte[]?>? images, string path) => (int)Invoke(null, "GenerateImage", images, path)!;
        Assert(SaveImages(null, "empty") == 0, "Null image list must save zero");
        Assert(SaveImages([null, []], "empty") == 0, "Empty bytes must save zero");
        Assert(SaveImages([png], "valid") == 1 && File.Exists("valid_1.png"), "Valid PNG must count a save");
        Assert(SaveImages([png, [1, 2, 3]], "partial") == 1, "Later invalid bytes must preserve earlier save count");
        Assert(SaveImages([png], Path.Combine("missing", "image")) == 0, "Failed save must count zero");
        int total = 0;
        Parallel.For(0, 4, i => Interlocked.Add(ref total, SaveImages([png, png], $"parallel-{i}")));
        Assert(total == 8, "Parallel aggregation must count all successful saves");
    }

    private static object? Invoke(object? target, string name, params object?[] args) =>
        typeof(GenerateSampleViewModel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)!.Invoke(target, args);

    private static void Save(GenerateSampleViewModel vm, string command) => Invoke(vm, "SaveCachedAfterCommand", command);
    private static void Equal(IEnumerable<string> actual, params string[] expected) => Assert(actual.SequenceEqual(expected), $"Unexpected persisted history/order: {JsonSerializer.Serialize(actual)}; expected {JsonSerializer.Serialize(expected)}");
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static void Wait(Task task)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) => { if (task.IsCompleted) frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        task.GetAwaiter().GetResult();
    }

    private delegate bool WindowCallback(nint window, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string className, string title);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(nint window, WindowCallback callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint window, StringBuilder text, int count);
    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);
}
