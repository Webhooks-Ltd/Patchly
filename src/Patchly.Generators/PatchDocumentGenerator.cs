using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Patchly.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class PatchDocumentGenerator : IIncrementalGenerator
{
    private static readonly string s_version =
        typeof(PatchDocumentGenerator).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var pipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Patchly.PatchDocumentAttribute",
            predicate: static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax,
            transform: static (ctx, ct) => TransformType(ctx, ct));

        context.RegisterSourceOutput(pipeline, static (spc, result) =>
        {
            foreach (var diag in result.Diagnostics)
                spc.ReportDiagnostic(diag.ToDiagnostic());

            if (result.Model is { } model)
                spc.AddSource($"{model.ClassName}.g.cs", GenerateSource(model));
        });

        var collected = pipeline
            .Where(static r => r.Model is not null)
            .Select(static (r, _) => r.Model!)
            .Collect()
            .Select(static (models, _) => new EquatableArray<PatchClassModel>(models));

        context.RegisterSourceOutput(collected, static (spc, models) =>
        {
            if (models.Length == 0)
                return;

            spc.AddSource("PatchlyJsonTypeInfoResolver.g.cs", GenerateResolver(models));
        });

        var hasAspNetCore = context.CompilationProvider
            .Select(static (compilation, _) =>
            {
                foreach (var asm in compilation.ReferencedAssemblyNames)
                {
                    if (asm.Name == "Microsoft.AspNetCore.Http")
                        return true;
                }
                return false;
            });

        var extensionInput = collected.Combine(hasAspNetCore);

        context.RegisterSourceOutput(extensionInput, static (spc, data) =>
        {
            var (models, hasAspNet) = data;
            if (models.Length == 0 || !hasAspNet)
                return;

            spc.AddSource("PatchlyServiceCollectionExtensions.g.cs", GenerateServiceCollectionExtensions());
        });
    }

    private static (PatchClassModel? Model, EquatableArray<DiagnosticInfo> Diagnostics) TransformType(
        GeneratorAttributeSyntaxContext ctx, System.Threading.CancellationToken ct)
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var symbol = (INamedTypeSymbol)ctx.TargetSymbol;
        var syntax = ctx.TargetNode;
        var location = syntax.GetLocation();
        var name = symbol.Name;
        var isDeterministicSemantics = IsDeterministicSemantics(ctx);

        try
        {
            if (symbol.TypeKind == TypeKind.Struct)
            {
                diagnostics.Add(DiagnosticInfo.Create(Diagnostics.AppliedToStruct, location, name));
                return (null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
            }

            if (symbol.IsRecord)
            {
                diagnostics.Add(DiagnosticInfo.Create(Diagnostics.AppliedToRecord, location, name));
                return (null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
            }

            if (symbol.IsAbstract)
            {
                diagnostics.Add(DiagnosticInfo.Create(Diagnostics.AppliedToAbstractClass, location, name));
                return (null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
            }

            if (symbol.TypeParameters.Length > 0)
            {
                diagnostics.Add(DiagnosticInfo.Create(Diagnostics.AppliedToGenericClass, location, name));
                return (null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
            }

            if (syntax is ClassDeclarationSyntax classDecl && !classDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                diagnostics.Add(DiagnosticInfo.Create(Diagnostics.NotPartialClass, location, name));
                return (null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
            }

            var jsonConstructors = new List<IMethodSymbol>();
            foreach (var ctor in symbol.Constructors)
            {
                if (ctor.IsImplicitlyDeclared || ctor.IsStatic) continue;
                foreach (var attr in ctor.GetAttributes())
                {
                    if (attr.AttributeClass?.Name == "JsonConstructorAttribute" &&
                        attr.AttributeClass.ContainingNamespace.ToDisplayString() == "System.Text.Json.Serialization")
                    {
                        jsonConstructors.Add(ctor);
                    }
                }
            }

            if (jsonConstructors.Count > 1)
            {
                diagnostics.Add(DiagnosticInfo.Create(Diagnostics.MultipleJsonConstructors, location, name));
                return (null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
            }

            var hasParameterlessCtor = HasAccessibleParameterlessConstructor(symbol);
            var jsonConstructor = jsonConstructors.Count == 1 ? jsonConstructors[0] : null;

            if (!hasParameterlessCtor && jsonConstructor == null)
            {
                diagnostics.Add(DiagnosticInfo.Create(Diagnostics.NoParameterlessConstructor, location, name));
                return (null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
            }

            var hasErrors = false;
            var properties = ImmutableArray.CreateBuilder<PatchPropertyModel>();
            var hasRequiredMembers = false;
            var hasInitOnlyProperties = false;

            foreach (var member in symbol.GetMembers())
            {
                ct.ThrowIfCancellationRequested();
                if (member is not IPropertySymbol prop || prop.IsStatic || prop.IsIndexer)
                    continue;

                var propAccessibility = GetAccessibility(prop);
                var hasJsonInclude = HasAttribute(prop, "System.Text.Json.Serialization.JsonIncludeAttribute");
                var isPublic = prop.DeclaredAccessibility == Accessibility.Public;

                if (!isPublic && !hasJsonInclude)
                    continue;

                var hasJsonIgnore = HasAttribute(prop, "System.Text.Json.Serialization.JsonIgnoreAttribute");
                var hasJsonExtensionData = HasAttribute(prop, "System.Text.Json.Serialization.JsonExtensionDataAttribute");

                if (hasJsonExtensionData)
                {
                    diagnostics.Add(DiagnosticInfo.Create(Diagnostics.JsonExtensionDataProperty, prop.Locations.FirstOrDefault() ?? location, prop.Name, name));
                    hasErrors = true;
                    continue;
                }

                var isReadOnly = prop.SetMethod == null;
                var isInitOnly = prop.SetMethod?.IsInitOnly == true;

                if (isReadOnly)
                {
                    diagnostics.Add(DiagnosticInfo.Create(Diagnostics.ReadOnlyProperty, prop.Locations.FirstOrDefault() ?? location, prop.Name, name));
                    continue;
                }

                if (isInitOnly && !hasJsonIgnore)
                    hasInitOnlyProperties = true;

                var isNonNullableValueType = prop.Type.IsValueType && prop.Type.NullableAnnotation != NullableAnnotation.Annotated &&
                                             prop.Type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T;

                if (isNonNullableValueType)
                {
                    diagnostics.Add(DiagnosticInfo.Create(Diagnostics.NonNullableValueType, prop.Locations.FirstOrDefault() ?? location, prop.Name, name));
                }

                var isNonNullableCollectionType = IsNonNullableCollectionType(prop);
                if (isDeterministicSemantics && isNonNullableCollectionType)
                {
                    diagnostics.Add(DiagnosticInfo.Create(Diagnostics.NonNullableCollectionTypeDeterministic, prop.Locations.FirstOrDefault() ?? location, prop.Name, name));
                }

                var jsonPropertyName = GetJsonPropertyName(prop);

                var hasJsonNumberHandling = false;
                string? jsonNumberHandlingValue = null;
                foreach (var attr in prop.GetAttributes())
                {
                    if (attr.AttributeClass?.Name == "JsonNumberHandlingAttribute" &&
                        attr.AttributeClass.ContainingNamespace.ToDisplayString() == "System.Text.Json.Serialization")
                    {
                        hasJsonNumberHandling = true;
                        if (attr.ConstructorArguments.Length > 0)
                            jsonNumberHandlingValue = attr.ConstructorArguments[0].Value?.ToString();
                    }
                }

                if (prop.IsRequired)
                    hasRequiredMembers = true;

                properties.Add(new PatchPropertyModel(
                    PropertyName: prop.Name,
                    TypeName: prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    JsonPropertyName: jsonPropertyName,
                    IsNullableValueType: prop.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T || prop.Type.NullableAnnotation == NullableAnnotation.Annotated && prop.Type.IsValueType,
                    IsNonNullableValueType: isNonNullableValueType,
                    IsNonNullableCollectionType: isNonNullableCollectionType,
                    HasJsonIgnore: hasJsonIgnore,
                    HasJsonInclude: hasJsonInclude,
                    HasJsonNumberHandling: hasJsonNumberHandling,
                    JsonNumberHandlingValue: jsonNumberHandlingValue,
                    IsReadOnly: isReadOnly,
                    IsInitOnly: isInitOnly,
                    HasJsonExtensionData: hasJsonExtensionData,
                    IsRequired: prop.IsRequired,
                    Accessibility: propAccessibility));
            }

            if (hasErrors)
                return (null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));

            var useBuffered = hasInitOnlyProperties || jsonConstructor != null;

            var trackedProperties = new List<PatchPropertyModel>();
            foreach (var p in properties)
            {
                if (!p.HasJsonIgnore) trackedProperties.Add(p);
            }

            EquatableArray<ConstructorParameterModel>? constructorParameters = null;

            if (jsonConstructor != null)
            {
                if (hasRequiredMembers)
                {
                    var hasSetsRequired = false;
                    foreach (var attr in jsonConstructor.GetAttributes())
                    {
                        if (attr.AttributeClass?.Name == "SetsRequiredMembersAttribute" &&
                            attr.AttributeClass.ContainingNamespace.ToDisplayString() == "System.Diagnostics.CodeAnalysis")
                        {
                            hasSetsRequired = true;
                            break;
                        }
                    }
                    if (!hasSetsRequired)
                    {
                        diagnostics.Add(DiagnosticInfo.Create(Diagnostics.JsonConstructorMissingSetsRequiredMembers, jsonConstructor.Locations.FirstOrDefault() ?? location, name));
                        return (null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
                    }
                }

                var ctorParams = ImmutableArray.CreateBuilder<ConstructorParameterModel>();
                foreach (var param in jsonConstructor.Parameters)
                {
                    string? matchedPropName = null;
                    string? matchedPropType = null;
                    foreach (var tracked in trackedProperties)
                    {
                        if (string.Equals(param.Name, tracked.PropertyName, StringComparison.OrdinalIgnoreCase))
                        {
                            matchedPropName = tracked.PropertyName;
                            matchedPropType = tracked.TypeName;
                            break;
                        }
                    }

                    if (matchedPropName == null)
                    {
                        diagnostics.Add(DiagnosticInfo.Create(Diagnostics.UnmatchedConstructorParameter, param.Locations.FirstOrDefault() ?? location, param.Name, name));
                    }
                    else
                    {
                        var paramType = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        if (paramType != matchedPropType)
                        {
                            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.ConstructorParameterTypeMismatch, param.Locations.FirstOrDefault() ?? location, param.Name, name, paramType, matchedPropType));
                            hasErrors = true;
                        }
                    }

                    string? defaultValueExpression = null;
                    if (param.HasExplicitDefaultValue)
                    {
                        var paramTypeName = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        defaultValueExpression = param.ExplicitDefaultValue switch
                        {
                            null => "null",
                            string s => $"\"{EscapeString(s)}\"",
                            bool b => b ? "true" : "false",
                            char c => $"'{EscapeChar(c)}'",
                            _ when IsEnumType(param.Type) => $"({paramTypeName}){param.ExplicitDefaultValue}",
                            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                            _ => param.ExplicitDefaultValue.ToString()
                        };
                    }

                    ctorParams.Add(new ConstructorParameterModel(
                        ParameterName: param.Name,
                        TypeName: param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        MatchedPropertyName: matchedPropName,
                        HasDefaultValue: param.HasExplicitDefaultValue,
                        DefaultValueExpression: defaultValueExpression));
                }

                if (hasErrors)
                    return (null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));

                foreach (var tracked in trackedProperties)
                {
                    if (!tracked.IsInitOnly) continue;
                    var coveredByCtor = false;
                    foreach (var cp in ctorParams)
                    {
                        if (cp.MatchedPropertyName == tracked.PropertyName)
                        {
                            coveredByCtor = true;
                            break;
                        }
                    }
                    if (!coveredByCtor)
                    {
                        diagnostics.Add(DiagnosticInfo.Create(Diagnostics.InitOnlyPropertyNotCoveredByConstructor, location, tracked.PropertyName, name));
                        hasErrors = true;
                    }
                }

                if (hasErrors)
                    return (null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));

                constructorParameters = new EquatableArray<ConstructorParameterModel>(ctorParams.ToImmutable());
            }

            if (useBuffered)
            {
                var reason = hasInitOnlyProperties && jsonConstructor != null
                    ? "init-only properties and a [JsonConstructor] constructor"
                    : hasInitOnlyProperties
                        ? "init-only properties"
                        : "a [JsonConstructor] constructor";
                diagnostics.Add(DiagnosticInfo.Create(Diagnostics.BufferedDeserialization, location, name, reason));
            }

            var hasTrackedProperties = trackedProperties.Count > 0;

            if (!hasTrackedProperties)
            {
                diagnostics.Add(DiagnosticInfo.Create(Diagnostics.NoPublicProperties, location, name));
            }

            var accessibility = symbol.DeclaredAccessibility switch
            {
                Microsoft.CodeAnalysis.Accessibility.Public => "public",
                Microsoft.CodeAnalysis.Accessibility.Internal => "internal",
                Microsoft.CodeAnalysis.Accessibility.Protected => "protected",
                Microsoft.CodeAnalysis.Accessibility.ProtectedOrInternal => "protected internal",
                Microsoft.CodeAnalysis.Accessibility.ProtectedAndInternal => "private protected",
                _ => "internal"
            };

            var ns = symbol.ContainingNamespace.IsGlobalNamespace
                ? ""
                : symbol.ContainingNamespace.ToDisplayString();

            var fullyQualifiedName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            var model = new PatchClassModel(
                ClassName: name,
                FullyQualifiedName: fullyQualifiedName,
                Namespace: ns,
                Accessibility: accessibility,
                IsDeterministicSemantics: isDeterministicSemantics,
                HasRequiredMembers: hasRequiredMembers,
                UseBufferedDeserialization: useBuffered,
                ConstructorParameters: constructorParameters,
                Properties: new EquatableArray<PatchPropertyModel>(properties.ToImmutable()));

            return (model, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.TypeSkipped, location, name));
            return (null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
        }
    }

    private static bool HasAccessibleParameterlessConstructor(INamedTypeSymbol symbol)
    {
        var hasInstanceCtor = false;
        foreach (var c in symbol.Constructors)
        {
            if (c.IsStatic) continue;
            hasInstanceCtor = true;
            if (c.Parameters.Length == 0 &&
                c.DeclaredAccessibility is Microsoft.CodeAnalysis.Accessibility.Public
                    or Microsoft.CodeAnalysis.Accessibility.Internal
                    or Microsoft.CodeAnalysis.Accessibility.Protected
                    or Microsoft.CodeAnalysis.Accessibility.ProtectedOrInternal)
            {
                return true;
            }
        }
        return !hasInstanceCtor;
    }

    private static bool IsDeterministicSemantics(GeneratorAttributeSyntaxContext ctx)
    {
        foreach (var attr in ctx.Attributes)
        {
            foreach (var namedArg in attr.NamedArguments)
            {
                if (!string.Equals(namedArg.Key, "SemanticsMode", StringComparison.Ordinal))
                    continue;

                if (namedArg.Value.Value is int enumValue)
                    return enumValue == 1;
            }
        }

        return false;
    }

    private static bool IsNonNullableCollectionType(IPropertySymbol prop)
    {
        if (prop.Type is IArrayTypeSymbol)
            return true;

        if (prop.Type.SpecialType == SpecialType.System_String)
            return false;

        if (!prop.Type.IsReferenceType)
            return false;

        if (prop.Type.NullableAnnotation == NullableAnnotation.Annotated)
            return false;

        if (prop.Type is not INamedTypeSymbol namedType)
            return false;

        foreach (var iface in namedType.AllInterfaces)
        {
            if (iface.ContainingNamespace.ToDisplayString() == "System.Collections" && iface.Name == "IEnumerable")
                return true;

            if (iface.ContainingNamespace.ToDisplayString() == "System.Collections.Generic" && iface.Name == "IEnumerable")
                return true;
        }

        return false;
    }

    private static bool HasAttribute(IPropertySymbol prop, string fullName)
    {
        foreach (var attr in prop.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass == null) continue;
            var full = attrClass.ContainingNamespace.ToDisplayString() + "." + attrClass.Name;
            if (full == fullName) return true;
        }
        return false;
    }

    private static string? GetJsonPropertyName(IPropertySymbol prop)
    {
        foreach (var attr in prop.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "JsonPropertyNameAttribute" &&
                attr.AttributeClass.ContainingNamespace.ToDisplayString() == "System.Text.Json.Serialization" &&
                attr.ConstructorArguments.Length > 0)
            {
                return attr.ConstructorArguments[0].Value?.ToString();
            }
        }
        return null;
    }

    private static string GetAccessibility(IPropertySymbol prop) =>
        prop.DeclaredAccessibility switch
        {
            Microsoft.CodeAnalysis.Accessibility.Public => "public",
            Microsoft.CodeAnalysis.Accessibility.Internal => "internal",
            Microsoft.CodeAnalysis.Accessibility.Protected => "protected",
            Microsoft.CodeAnalysis.Accessibility.ProtectedOrInternal => "protected internal",
            Microsoft.CodeAnalysis.Accessibility.ProtectedAndInternal => "private protected",
            Microsoft.CodeAnalysis.Accessibility.Private => "private",
            _ => "public"
        };

    private static string GenerateSource(PatchClassModel model)
    {
        var sb = new StringBuilder();
        var tracked = new List<PatchPropertyModel>();
        foreach (var p in model.Properties)
        {
            if (!p.HasJsonIgnore) tracked.Add(p);
        }

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(model.Namespace))
        {
            sb.AppendLine($"namespace {model.Namespace};");
            sb.AppendLine();
        }

        sb.AppendLine($"[System.CodeDom.Compiler.GeneratedCode(\"Patchly.Generators\", \"{s_version}\")]");
        sb.AppendLine($"[System.Text.Json.Serialization.JsonConverter(typeof({model.ClassName}.{model.ClassName}JsonConverter))]");
        sb.AppendLine($"{model.Accessibility} partial class {model.ClassName} : Patchly.IPatchDocument");
        sb.AppendLine("{");

        if (model.HasRequiredMembers && model.ConstructorParameters == null)
        {
            sb.AppendLine("    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]");
            sb.AppendLine($"    internal {model.ClassName}(bool _) {{ }}");
            sb.AppendLine();
        }

        sb.AppendLine("    [System.Text.Json.Serialization.JsonIgnore]");
        sb.AppendLine("    private readonly System.Collections.Generic.HashSet<string> _providedProperties = new(System.StringComparer.OrdinalIgnoreCase);");
        sb.AppendLine();

        sb.AppendLine("    public bool WasProvided(string propertyName) => _providedProperties.Contains(propertyName);");
        sb.AppendLine();

        GenerateGetStateMethod(sb, model.ClassName, tracked);
        sb.AppendLine();

        sb.AppendLine("    [System.Text.Json.Serialization.JsonIgnore]");
        sb.AppendLine("    public System.Collections.Generic.IReadOnlySet<string> ProvidedProperties => _providedProperties;");
        sb.AppendLine();

        sb.AppendLine("    [System.Text.Json.Serialization.JsonIgnore]");
        sb.AppendLine($"    public ProvidedSet Provided => new ProvidedSet(_providedProperties);");
        sb.AppendLine();

        sb.AppendLine("    internal void MarkProvided(string name) => _providedProperties.Add(name);");
        sb.AppendLine();

        GenerateProvidedSet(sb, model.ClassName, tracked);
        sb.AppendLine();

        if (model.IsDeterministicSemantics)
        {
            GenerateStateSet(sb, model.ClassName, tracked);
            sb.AppendLine();
        }

        GenerateJsonConverter(sb, model, tracked);

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateResolver(EquatableArray<PatchClassModel> models)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine();
        sb.AppendLine("namespace Patchly;");
        sb.AppendLine();
        sb.AppendLine($"[System.CodeDom.Compiler.GeneratedCode(\"Patchly.Generators\", \"{s_version}\")]");
        sb.AppendLine("internal sealed class PatchlyJsonTypeInfoResolver : System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver");
        sb.AppendLine("{");
        sb.AppendLine("    public static PatchlyJsonTypeInfoResolver Default { get; } = new();");
        sb.AppendLine();
        sb.AppendLine("    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(\"AOT\", \"IL3050\", Justification = \"All types are statically known at source-generation time.\")]");
        sb.AppendLine("    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(\"Trimming\", \"IL2026\", Justification = \"All types are statically known at source-generation time.\")]");
        sb.AppendLine("    public System.Text.Json.Serialization.Metadata.JsonTypeInfo? GetTypeInfo(System.Type type, System.Text.Json.JsonSerializerOptions options)");
        sb.AppendLine("    {");

        for (var i = 0; i < models.Length; i++)
        {
            var model = models[i];
            var fqn = model.FullyQualifiedName;
            var elsePrefix = i == 0 ? "" : "else ";
            sb.AppendLine($"        {elsePrefix}if (type == typeof({fqn}))");
            sb.AppendLine("        {");

            if (model.UseBufferedDeserialization)
            {
                sb.AppendLine($"            return System.Text.Json.Serialization.Metadata.JsonMetadataServices.CreateValueInfo<{fqn}>(options, new {fqn}.{model.ClassName}JsonConverter());");
            }
            else
            {
                sb.AppendLine($"            var typeInfo = System.Text.Json.Serialization.Metadata.JsonTypeInfo.CreateJsonTypeInfo<{fqn}>(options);");

                if (model.HasRequiredMembers)
                    sb.AppendLine($"            typeInfo.CreateObject = static () => new {fqn}(false);");
                else
                    sb.AppendLine($"            typeInfo.CreateObject = static () => new {fqn}();");
                sb.AppendLine();

                var tracked = new List<PatchPropertyModel>();
                foreach (var p in model.Properties)
                {
                    if (!p.HasJsonIgnore) tracked.Add(p);
                }

                foreach (var prop in tracked)
                {
                    var typeofName = GetTypeofSafeTypeName(prop);

                    sb.AppendLine("            {");

                    if (prop.JsonPropertyName != null)
                    {
                        sb.AppendLine($"                var prop = typeInfo.CreateJsonPropertyInfo(typeof({typeofName}), \"{EscapeString(prop.JsonPropertyName)}\");");
                    }
                    else
                    {
                        sb.AppendLine($"                var jsonName = options.PropertyNamingPolicy?.ConvertName(\"{prop.PropertyName}\") ?? \"{prop.PropertyName}\";");
                        sb.AppendLine($"                var prop = typeInfo.CreateJsonPropertyInfo(typeof({typeofName}), jsonName);");
                    }

                    sb.AppendLine($"                prop.Get = static obj => (({fqn})obj!).{prop.PropertyName};");
                    sb.AppendLine($"                prop.Set = static (obj, val) => {{ var t = ({fqn})obj!; t.{prop.PropertyName} = ({prop.TypeName})val!; t.MarkProvided(\"{prop.PropertyName}\"); }};");

                    if (prop.HasJsonNumberHandling && prop.JsonNumberHandlingValue != null)
                    {
                        sb.AppendLine($"                prop.NumberHandling = (System.Text.Json.Serialization.JsonNumberHandling){prop.JsonNumberHandlingValue};");
                    }

                    sb.AppendLine("                typeInfo.Properties.Add(prop);");
                    sb.AppendLine("            }");
                }

                sb.AppendLine();
                sb.AppendLine("            return typeInfo;");
            }

            sb.AppendLine("        }");
        }

        sb.AppendLine();
        sb.AppendLine("        return null;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("#endif");

        return sb.ToString();
    }

    private static string GenerateServiceCollectionExtensions()
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine();
        sb.AppendLine("namespace Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine("internal static partial class PatchlyServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddPatchly(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        services.ConfigureHttpJsonOptions(static o =>");
        sb.AppendLine("            o.SerializerOptions.TypeInfoResolverChain.Insert(0, global::Patchly.PatchlyJsonTypeInfoResolver.Default));");
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("#endif");
        return sb.ToString();
    }

    private static void GenerateProvidedSet(StringBuilder sb, string className, List<PatchPropertyModel> tracked)
    {
        sb.AppendLine("    /// <summary>Indicates which properties were present in the JSON payload.</summary>");
        sb.AppendLine("    public readonly struct ProvidedSet");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly System.Collections.Generic.HashSet<string>? _set;");
        sb.AppendLine();
        sb.AppendLine("        internal ProvidedSet(System.Collections.Generic.HashSet<string>? set) => _set = set;");
        sb.AppendLine();

        foreach (var prop in tracked)
        {
            sb.AppendLine($"        /// <summary>Returns <c>true</c> if <see cref=\"{className}.{prop.PropertyName}\"/> was present in the JSON payload.</summary>");
            sb.AppendLine($"        public bool {prop.PropertyName} => _set?.Contains(nameof({className}.{prop.PropertyName})) ?? false;");
        }

        sb.AppendLine("    }");
    }

    private static void GenerateGetStateMethod(StringBuilder sb, string className, List<PatchPropertyModel> tracked)
    {
        sb.AppendLine("    public Patchly.PatchValueState GetState(string propertyName)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (!_providedProperties.Contains(propertyName))");
        sb.AppendLine("            return Patchly.PatchValueState.Omitted;");
        sb.AppendLine();

        foreach (var prop in tracked)
        {
            sb.AppendLine($"        if (string.Equals(propertyName, nameof({className}.{prop.PropertyName}), System.StringComparison.OrdinalIgnoreCase))");
            if (prop.IsNonNullableValueType)
                sb.AppendLine("            return Patchly.PatchValueState.Value;");
            else
                sb.AppendLine($"            return {prop.PropertyName} is null ? Patchly.PatchValueState.Null : Patchly.PatchValueState.Value;");
        }

        sb.AppendLine();
        sb.AppendLine("        return Patchly.PatchValueState.Omitted;");
        sb.AppendLine("    }");
    }

    private static void GenerateStateSet(StringBuilder sb, string className, List<PatchPropertyModel> tracked)
    {
        sb.AppendLine("    [System.Text.Json.Serialization.JsonIgnore]");
        sb.AppendLine("    public StateSet State => new StateSet(this);");
        sb.AppendLine();
        sb.AppendLine("    public readonly struct StateSet");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {className} _owner;");
        sb.AppendLine();
        sb.AppendLine($"        internal StateSet({className} owner) => _owner = owner;");
        sb.AppendLine();

        foreach (var prop in tracked)
        {
            sb.AppendLine($"        public Patchly.PatchValueState {prop.PropertyName} => _owner.GetState(nameof({className}.{prop.PropertyName}));");
        }

        sb.AppendLine("    }");
    }

    private static void GenerateJsonConverter(StringBuilder sb, PatchClassModel model, List<PatchPropertyModel> tracked)
    {
        var className = model.ClassName;
        sb.AppendLine($"    internal sealed class {className}JsonConverter : System.Text.Json.Serialization.JsonConverter<{className}>");
        sb.AppendLine("    {");

        sb.AppendLine($"        public override {className}? Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (reader.TokenType == System.Text.Json.JsonTokenType.Null)");
        sb.AppendLine("                return null;");
        sb.AppendLine();
        sb.AppendLine("            if (reader.TokenType != System.Text.Json.JsonTokenType.StartObject)");
        sb.AppendLine("                throw new System.Text.Json.JsonException($\"Expected StartObject, got {reader.TokenType}\");");
        sb.AppendLine();

        if (model.UseBufferedDeserialization)
            GenerateBufferedReadBody(sb, model, tracked);
        else
            GenerateStreamingReadBody(sb, model, tracked);

        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine($"        public override void Write(System.Text.Json.Utf8JsonWriter writer, {className} value, System.Text.Json.JsonSerializerOptions options)");
        sb.AppendLine("        {");
        sb.AppendLine("            writer.WriteStartObject();");
        sb.AppendLine();

        foreach (var prop in tracked)
        {
            var jsonName = prop.JsonPropertyName != null
                ? $"\"{EscapeString(prop.JsonPropertyName)}\""
                : $"ResolvePropertyName(nameof({className}.{prop.PropertyName}), null, options)";

            sb.AppendLine($"            {{");
            sb.AppendLine($"                var propName = {jsonName};");
            sb.AppendLine($"                var propValue = value.{prop.PropertyName};");

            var writeTypeofName = GetTypeofSafeTypeName(prop);

            sb.AppendLine($"                if (ShouldWriteProperty(propValue, options))");
            sb.AppendLine($"                {{");
            sb.AppendLine($"                    writer.WritePropertyName(propName);");
            sb.AppendLine($"#if NET8_0_OR_GREATER");
            sb.AppendLine($"                    System.Text.Json.JsonSerializer.Serialize(writer, propValue!, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<{writeTypeofName}>)options.GetTypeInfo(typeof({writeTypeofName})));");
            sb.AppendLine($"#else");
            sb.AppendLine($"                    System.Text.Json.JsonSerializer.Serialize(writer, propValue, options);");
            sb.AppendLine($"#endif");
            sb.AppendLine($"                }}");
            sb.AppendLine($"            }}");
        }

        sb.AppendLine();
        sb.AppendLine("            writer.WriteEndObject();");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        private static bool MatchesPropertyName(string jsonName, string csharpName, string? explicitJsonName, System.Text.Json.JsonSerializerOptions options)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (explicitJsonName != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                return options.PropertyNameCaseInsensitive");
        sb.AppendLine("                    ? string.Equals(jsonName, explicitJsonName, System.StringComparison.OrdinalIgnoreCase)");
        sb.AppendLine("                    : string.Equals(jsonName, explicitJsonName, System.StringComparison.Ordinal);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var expectedName = options.PropertyNamingPolicy?.ConvertName(csharpName) ?? csharpName;");
        sb.AppendLine("            return options.PropertyNameCaseInsensitive");
        sb.AppendLine("                ? string.Equals(jsonName, expectedName, System.StringComparison.OrdinalIgnoreCase)");
        sb.AppendLine("                : string.Equals(jsonName, expectedName, System.StringComparison.Ordinal);");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        private static string ResolvePropertyName(string csharpName, string? explicitJsonName, System.Text.Json.JsonSerializerOptions options)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (explicitJsonName != null) return explicitJsonName;");
        sb.AppendLine("            return options.PropertyNamingPolicy?.ConvertName(csharpName) ?? csharpName;");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        private static bool ShouldWriteProperty<T>(T value, System.Text.Json.JsonSerializerOptions options)");
        sb.AppendLine("        {");
        sb.AppendLine("            return options.DefaultIgnoreCondition switch");
        sb.AppendLine("            {");
        sb.AppendLine("                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull => value is not null,");
        sb.AppendLine("                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault => !System.Collections.Generic.EqualityComparer<T>.Default.Equals(value, default!),");
        sb.AppendLine("                _ => true,");
        sb.AppendLine("            };");
        sb.AppendLine("        }");

        sb.AppendLine("    }");
    }

    private static void GenerateStreamingReadBody(StringBuilder sb, PatchClassModel model, List<PatchPropertyModel> tracked)
    {
        var className = model.ClassName;

        if (model.HasRequiredMembers)
            sb.AppendLine($"            var result = new {className}(false);");
        else
            sb.AppendLine($"            var result = new {className}();");
        sb.AppendLine();

        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.TokenType == System.Text.Json.JsonTokenType.EndObject)");
        sb.AppendLine("                    return result;");
        sb.AppendLine();
        sb.AppendLine("                if (reader.TokenType != System.Text.Json.JsonTokenType.PropertyName)");
        sb.AppendLine("                    throw new System.Text.Json.JsonException($\"Expected PropertyName, got {reader.TokenType}\");");
        sb.AppendLine();
        sb.AppendLine("                var propertyName = reader.GetString()!;");
        sb.AppendLine("                var matched = false;");
        sb.AppendLine();

        for (var i = 0; i < tracked.Count; i++)
        {
            var prop = tracked[i];
            var elsePrefix = i == 0 ? "" : "else ";

            sb.AppendLine($"                {elsePrefix}if (MatchesPropertyName(propertyName, nameof({className}.{prop.PropertyName}), {(prop.JsonPropertyName != null ? $"\"{EscapeString(prop.JsonPropertyName)}\"" : "null")}, options))");
            sb.AppendLine("                {");
            sb.AppendLine("                    reader.Read();");

            EmitDeserializeProperty(sb, prop, $"result.{prop.PropertyName}");

            sb.AppendLine($"                    result._providedProperties.Add(nameof({className}.{prop.PropertyName}));");
            sb.AppendLine("                    matched = true;");
            sb.AppendLine("                }");
        }

        sb.AppendLine();
        sb.AppendLine("                if (!matched)");
        sb.AppendLine("                {");
        sb.AppendLine("                    reader.Read();");
        sb.AppendLine("                    reader.Skip();");
        sb.AppendLine("                }");

        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            return result;");
    }

    private static void GenerateBufferedReadBody(StringBuilder sb, PatchClassModel model, List<PatchPropertyModel> tracked)
    {
        var className = model.ClassName;

        foreach (var prop in tracked)
        {
            var localName = $"_local_{prop.PropertyName}";
            var flagName = $"_provided_{prop.PropertyName}";
            sb.AppendLine($"            {prop.TypeName} {localName} = default!;");
            sb.AppendLine($"            var {flagName} = false;");
        }
        sb.AppendLine();

        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.TokenType == System.Text.Json.JsonTokenType.EndObject)");
        sb.AppendLine("                    break;");
        sb.AppendLine();
        sb.AppendLine("                if (reader.TokenType != System.Text.Json.JsonTokenType.PropertyName)");
        sb.AppendLine("                    throw new System.Text.Json.JsonException($\"Expected PropertyName, got {reader.TokenType}\");");
        sb.AppendLine();
        sb.AppendLine("                var propertyName = reader.GetString()!;");
        sb.AppendLine("                var matched = false;");
        sb.AppendLine();

        for (var i = 0; i < tracked.Count; i++)
        {
            var prop = tracked[i];
            var elsePrefix = i == 0 ? "" : "else ";
            var localName = $"_local_{prop.PropertyName}";
            var flagName = $"_provided_{prop.PropertyName}";

            sb.AppendLine($"                {elsePrefix}if (MatchesPropertyName(propertyName, nameof({className}.{prop.PropertyName}), {(prop.JsonPropertyName != null ? $"\"{EscapeString(prop.JsonPropertyName)}\"" : "null")}, options))");
            sb.AppendLine("                {");
            sb.AppendLine("                    reader.Read();");

            EmitDeserializeProperty(sb, prop, localName);

            sb.AppendLine($"                    {flagName} = true;");
            sb.AppendLine("                    matched = true;");
            sb.AppendLine("                }");
        }

        sb.AppendLine();
        sb.AppendLine("                if (!matched)");
        sb.AppendLine("                {");
        sb.AppendLine("                    reader.Read();");
        sb.AppendLine("                    reader.Skip();");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();

        if (model.ConstructorParameters != null)
        {
            var ctorArgs = new List<string>();
            var ctorParams = model.ConstructorParameters.Value;
            foreach (var cp in ctorParams)
            {
                if (cp.MatchedPropertyName != null)
                {
                    if (cp.HasDefaultValue)
                        ctorArgs.Add($"_provided_{cp.MatchedPropertyName} ? _local_{cp.MatchedPropertyName} : {cp.DefaultValueExpression}");
                    else
                        ctorArgs.Add($"_local_{cp.MatchedPropertyName}");
                }
                else
                {
                    ctorArgs.Add(cp.HasDefaultValue ? cp.DefaultValueExpression! : $"default({cp.TypeName})");
                }
            }

            sb.AppendLine($"            var result = new {className}({string.Join(", ", ctorArgs)});");

            var coveredByConstructor = new HashSet<string>();
            foreach (var cp in ctorParams)
            {
                if (cp.MatchedPropertyName != null)
                    coveredByConstructor.Add(cp.MatchedPropertyName);
            }

            foreach (var prop in tracked)
            {
                if (coveredByConstructor.Contains(prop.PropertyName)) continue;
                if (prop.IsInitOnly) continue;
                sb.AppendLine($"            if (_provided_{prop.PropertyName}) result.{prop.PropertyName} = _local_{prop.PropertyName};");
            }
        }
        else
        {
            // Init-only props must appear in the object initializer — safe to assign unconditionally
            // because all tracked properties are nullable (enforced by PATCH010).
            var initProps = new List<PatchPropertyModel>();
            foreach (var prop in tracked)
            {
                if (prop.IsInitOnly) initProps.Add(prop);
            }

            if (initProps.Count > 0)
            {
                if (model.HasRequiredMembers)
                    sb.Append($"            var result = new {className}(false) {{ ");
                else
                    sb.Append($"            var result = new {className} {{ ");

                var first = true;
                foreach (var prop in initProps)
                {
                    if (!first) sb.Append(", ");
                    sb.Append($"{prop.PropertyName} = _local_{prop.PropertyName}");
                    first = false;
                }
                sb.AppendLine(" };");
            }
            else
            {
                if (model.HasRequiredMembers)
                    sb.AppendLine($"            var result = new {className}(false);");
                else
                    sb.AppendLine($"            var result = new {className}();");
            }

            foreach (var prop in tracked)
            {
                if (prop.IsInitOnly) continue;
                sb.AppendLine($"            if (_provided_{prop.PropertyName}) result.{prop.PropertyName} = _local_{prop.PropertyName};");
            }
        }

        sb.AppendLine();
        foreach (var prop in tracked)
        {
            sb.AppendLine($"            if (_provided_{prop.PropertyName}) result._providedProperties.Add(nameof({className}.{prop.PropertyName}));");
        }
        sb.AppendLine();
        sb.AppendLine("            return result;");
    }

    private static void EmitDeserializeProperty(StringBuilder sb, PatchPropertyModel prop, string target)
    {
        var typeofName = GetTypeofSafeTypeName(prop);

        if (prop.HasJsonNumberHandling && prop.JsonNumberHandlingValue != null)
        {
            sb.AppendLine($"                    var propOptions = new System.Text.Json.JsonSerializerOptions(options);");
            sb.AppendLine($"                    propOptions.NumberHandling = (System.Text.Json.Serialization.JsonNumberHandling){prop.JsonNumberHandlingValue};");
            sb.AppendLine($"#if NET8_0_OR_GREATER");
            sb.AppendLine($"                    {target} = System.Text.Json.JsonSerializer.Deserialize(ref reader, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<{typeofName}>)propOptions.GetTypeInfo(typeof({typeofName})))!;");
            sb.AppendLine($"#else");
            sb.AppendLine($"                    {target} = System.Text.Json.JsonSerializer.Deserialize<{prop.TypeName}>(ref reader, propOptions)!;");
            sb.AppendLine($"#endif");
        }
        else
        {
            sb.AppendLine($"#if NET8_0_OR_GREATER");
            sb.AppendLine($"                    {target} = System.Text.Json.JsonSerializer.Deserialize(ref reader, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<{typeofName}>)options.GetTypeInfo(typeof({typeofName})))!;");
            sb.AppendLine($"#else");
            sb.AppendLine($"                    {target} = System.Text.Json.JsonSerializer.Deserialize<{prop.TypeName}>(ref reader, options)!;");
            sb.AppendLine($"#endif");
        }
    }

    private static string GetTypeofSafeTypeName(PatchPropertyModel prop)
    {
        if (prop.IsNullableValueType)
            return prop.TypeName;
        if (prop.TypeName.EndsWith("?"))
            return prop.TypeName.Substring(0, prop.TypeName.Length - 1);
        return prop.TypeName;
    }

    private static string EscapeString(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"")
         .Replace("\n", "\\n").Replace("\r", "\\r")
         .Replace("\t", "\\t").Replace("\0", "\\0");

    private static string EscapeChar(char c) => c switch
    {
        '\\' => "\\\\",
        '\'' => "\\'",
        '\n' => "\\n",
        '\r' => "\\r",
        '\t' => "\\t",
        '\0' => "\\0",
        _ => c.ToString()
    };

    private static bool IsEnumType(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum) return true;
        if (type is INamedTypeSymbol named &&
            named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            named.TypeArguments.Length == 1 &&
            named.TypeArguments[0].TypeKind == TypeKind.Enum)
            return true;
        return false;
    }
}
