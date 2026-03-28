namespace Patchly;

/// <summary>
/// Controls how Patchly handles unrecognized JSON properties during deserialization.
/// </summary>
public enum UnknownPropertyHandling
{
    /// <summary>
    /// Silently ignore unrecognized JSON properties.
    /// </summary>
    Ignore = 0,

    /// <summary>
    /// Reject payloads containing unrecognized JSON properties.
    /// </summary>
    Reject = 1
}
