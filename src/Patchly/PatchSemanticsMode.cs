namespace Patchly;

/// <summary>
/// Controls generated patch semantics behavior.
/// </summary>
public enum PatchSemanticsMode
{
    /// <summary>Use legacy behavior.</summary>
    Legacy = 0,
    /// <summary>Enable deterministic tri-state semantics and related diagnostics.</summary>
    DeterministicV1 = 1
}
