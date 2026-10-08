using WinKeyCastr.Input;
using WinKeyCastr.Settings;

namespace WinKeyCastr.Visualizers;

/// <summary>Mirrors KeyCastr's KCVisualizer protocol.</summary>
public interface IVisualizer : IDisposable
{
    string Name { get; }

    void Show();
    void Hide();

    void NoteKeyDown(KeyStroke keystroke);

    /// <summary>A key was released; null when the key-up may have been lost (focus moved elsewhere).</summary>
    void NoteKeyUp(KeyStroke? keystroke) { }

    void NoteFlagsChanged(ModifierFlags flags);
    void NoteMouse(MouseEventInfo mouseEvent);
}

/// <summary>What every visualizer needs from the application.</summary>
public sealed class VisualizerContext
{
    public required AppSettings Settings { get; init; }
    public required KeystrokeTransformer Transformer { get; init; }

    /// <summary>Persist settings (positions are saved this way after a drag).</summary>
    public required Action SaveSettings { get; init; }
}

public static class VisualizerRegistry
{
    public static readonly IReadOnlyList<string> Names = ["Default", "Svelte", "Minimal"];

    public static IVisualizer Create(string name, VisualizerContext context) => name switch
    {
        "Svelte" => new SvelteVisualizer(context),
        "Minimal" => new MinimalVisualizer(context),
        _ => new DefaultVisualizer(context),
    };
}
