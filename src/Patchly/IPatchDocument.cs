namespace Patchly;

/// <summary>
/// Implemented by all <see cref="PatchDocumentAttribute"/>-annotated classes.
/// Provides runtime access to which JSON fields were present in the request.
/// </summary>
public interface IPatchDocument
{
    /// <summary>Returns <c>true</c> if the named property was present in the JSON payload.</summary>
    bool WasProvided(string propertyName);

    /// <summary>The set of property names that were present in the JSON payload.</summary>
    IReadOnlySet<string> ProvidedProperties { get; }
}
