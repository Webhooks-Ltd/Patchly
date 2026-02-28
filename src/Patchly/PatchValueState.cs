namespace Patchly;

/// <summary>
/// Represents whether a patch property was omitted, explicitly set to null, or set to a value.
/// </summary>
public enum PatchValueState
{
    /// <summary>The property was not present in the JSON payload.</summary>
    Omitted = 0,
    /// <summary>The property was present in the JSON payload with a null value.</summary>
    Null = 1,
    /// <summary>The property was present in the JSON payload with a non-null value.</summary>
    Value = 2
}
