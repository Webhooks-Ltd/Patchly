namespace Patchly.Generators;

internal sealed record PatchClassModel(
    string ClassName,
    string FullyQualifiedName,
    string Namespace,
    string Accessibility,
    bool HasRequiredMembers,
    bool UseBufferedDeserialization,
    EquatableArray<ConstructorParameterModel>? ConstructorParameters,
    EquatableArray<PatchPropertyModel> Properties);
