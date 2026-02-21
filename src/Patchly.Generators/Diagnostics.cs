using Microsoft.CodeAnalysis;

namespace Patchly.Generators;

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor NotPartialClass = new(
        "PATCH001",
        "[PatchDocument] class must be partial",
        "Class '{0}' must be declared as partial to use [PatchDocument]",
        "Patchly",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AppliedToStruct = new(
        "PATCH002",
        "[PatchDocument] cannot be applied to a struct",
        "'{0}' is a struct; [PatchDocument] can only be applied to a partial class",
        "Patchly",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AppliedToRecord = new(
        "PATCH003",
        "[PatchDocument] cannot be applied to a record",
        "'{0}' is a record; [PatchDocument] can only be applied to a partial class",
        "Patchly",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AppliedToAbstractClass = new(
        "PATCH004",
        "[PatchDocument] cannot be applied to an abstract class",
        "'{0}' is abstract; [PatchDocument] classes must be concrete",
        "Patchly",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AppliedToGenericClass = new(
        "PATCH005",
        "[PatchDocument] cannot be applied to a generic class",
        "'{0}' is generic; [PatchDocument] classes must not have type parameters",
        "Patchly",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoParameterlessConstructor = new(
        "PATCH006",
        "[PatchDocument] class must have an accessible parameterless constructor",
        "'{0}' has no accessible parameterless constructor; [PatchDocument] classes require one for deserialization",
        "Patchly",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NonNullableValueType = new(
        "PATCH010",
        "Non-nullable value type property on [PatchDocument]",
        "Property '{0}' on '{1}' is a non-nullable value type; it cannot distinguish between 'not provided' and 'default value'",
        "Patchly",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoPublicProperties = new(
        "PATCH011",
        "[PatchDocument] class has no public properties",
        "'{0}' has no public properties to track",
        "Patchly",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ReadOnlyProperty = new(
        "PATCH012",
        "Read-only property on [PatchDocument]",
        "Property '{0}' on '{1}' is read-only and will be excluded from deserialization and the Provided accessor",
        "Patchly",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InitOnlyProperty = new(
        "PATCH013",
        "Init-only property on [PatchDocument]",
        "Property '{0}' on '{1}' is init-only; [PatchDocument] classes do not support init-only properties because the generated converter cannot set them after construction",
        "Patchly",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor JsonExtensionDataProperty = new(
        "PATCH014",
        "[JsonExtensionData] is not supported on [PatchDocument]",
        "Property '{0}' on '{1}' has [JsonExtensionData] which is not supported on [PatchDocument] classes",
        "Patchly",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor JsonConstructorIgnored = new(
        "PATCH015",
        "[JsonConstructor] is ignored by Patchly",
        "Constructor on '{0}' has [JsonConstructor] which is ignored by the Patchly-generated converter",
        "Patchly",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicatePatchMap = new(
        "PATCH020",
        "Duplicate PatchMap for the same (TPatch, TTarget) pair",
        "Multiple PatchMap classes ({0}) map the same pair {1}; only one map per (TPatch, TTarget) pair is allowed",
        "Patchly",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TypeSkipped = new(
        "PATCH099",
        "[PatchDocument] type was skipped",
        "'{0}' was skipped due to unresolvable errors",
        "Patchly",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
