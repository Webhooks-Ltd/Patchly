namespace Patchly;

public interface IPatchDocument
{
    bool WasProvided(string propertyName);
    IReadOnlySet<string> ProvidedProperties { get; }
}
