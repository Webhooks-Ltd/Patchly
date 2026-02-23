namespace Patchly.Generators;

internal sealed record ConstructorParameterModel(
    string ParameterName,
    string TypeName,
    string? MatchedPropertyName,
    bool HasDefaultValue,
    string? DefaultValueExpression) : IEquatable<ConstructorParameterModel>;
