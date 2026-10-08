#if DEBUG
using System.Windows.Threading;
using WinKeyCastr.Input;
using WinKeyCastr.Settings;

namespace WinKeyCastr;

/// <summary>
/// Debug-only scripted input for checking the visualizers without touching real input:
/// <c>WinKeyCastr.exe --demo Default|Svelte|Minimal</c>. Settings are not saved in demo mode.
/// </summary>
internal static class Demo
{
    public static string? RequestedVisualizer(string[] args)
    {
        int i = Array.IndexOf(args, "--demo");
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    public static void Run(AppController controller, AppSettings settings)
    {
        var steps = new List<(double At, Action Action)>();
        double t = 0.5;

        void Key(string keyCap, int vk, ModifierFlags mods = ModifierFlags.None, double gap = 0.12, bool hold = false)
        {
            var down = Stroke(keyCap, vk, mods);
            steps.Add((t, () => controller.SimulateKeyDown(down)));
            if (!hold)
                steps.Add((t + 0.08, () => controller.SimulateKeyUp(down)));
            t += gap;
        }

        void Flags(ModifierFlags mods, double gap = 0.05)
        {
            steps.Add((t, () => controller.SimulateFlags(mods)));
            t += gap;
        }

        void Type(string text)
        {
            foreach (char c in text)
                Key(c.ToString(), char.ToUpperInvariant(c));
        }

        Type("hello");
        t += 0.8;
        Flags(ModifierFlags.Control | ModifierFlags.Shift);
        Key("k", 'K', ModifierFlags.Control | ModifierFlags.Shift);
        Flags(ModifierFlags.None);
        t += 0.3;
        Flags(ModifierFlags.Command);
        Key("e", 'E', ModifierFlags.Command, hold: true);
        t += 0.6;
        Flags(ModifierFlags.None);
        Key("", 0x08);
        Key("", 0x0D);
        Flags(ModifierFlags.Shift);
        Key("a", 'A', ModifierFlags.Shift);
        Flags(ModifierFlags.None);
        Key("", 0x25);
        Key("", 0x1B);
        steps.Add((t, () => controller.SimulateMouse(MouseEventKind.Down)));
        steps.Add((t + 0.15, () => controller.SimulateMouse(MouseEventKind.Up)));
        t += 0.4;
        Flags(ModifierFlags.Option);
        Key("", 0x73, ModifierFlags.Option, hold: true);

        var start = DateTime.UtcNow;
        int next = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) =>
        {
            double elapsed = (DateTime.UtcNow - start).TotalSeconds;
            while (next < steps.Count && steps[next].At <= elapsed)
                steps[next++].Action();
            if (next >= steps.Count)
                timer.Stop();
        };
        timer.Start();
    }

    private static KeyStroke Stroke(string keyCap, int vk, ModifierFlags mods) => new()
    {
        VirtualKey = vk,
        ScanCode = 0,
        IsExtended = false,
        Modifiers = mods,
        IsRepeat = false,
        KeyCap = keyCap,
        Characters = mods.HasFlag(ModifierFlags.Shift) ? keyCap.ToUpperInvariant() : keyCap,
    };
}
#endif
