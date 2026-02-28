namespace Patchly.Generators;

internal sealed record PatchPropertyModel(
    string PropertyName,
    string TypeName,
    string? JsonPropertyName,
    bool IsNullableValueType,
    bool IsNonNullableValueType,
    bool IsNonNullableCollectionType,
    bool HasJsonIgnore,
    bool HasJsonInclude,
    bool HasJsonNumberHandling,
    string? JsonNumberHandlingValue,
    bool IsReadOnly,
    bool IsInitOnly,
    bool HasJsonExtensionData,
    bool IsRequired,
    string Accessibility);
