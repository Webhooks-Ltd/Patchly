namespace Patchly.Generators;

internal sealed record PatchMapModel(
    string ClassName,
    string FullyQualifiedName,
    string PatchFullyQualifiedName,
    string TargetFullyQualifiedName);
