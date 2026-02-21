namespace Patchly.Generators;

internal sealed record PatchClassModel(
    string ClassName,
    string FullyQualifiedName,
    string Namespace,
    string Accessibility,
    bool HasRequiredMembers,
    EquatableArray<PatchPropertyModel> Properties);
