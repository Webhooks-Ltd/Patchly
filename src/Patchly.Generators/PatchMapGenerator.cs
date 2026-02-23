using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Patchly.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class PatchMapGenerator : IIncrementalGenerator
{
    private static readonly string s_version =
        typeof(PatchMapGenerator).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var pipeline = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => IsCandidateClass(node),
            transform: static (ctx, ct) => TransformMap(ctx, ct));

        var collected = pipeline
            .Where(static r => r.Model is not null || r.Diagnostics.Length > 0)
            .Collect()
            .Select(static (results, _) =>
            {
                var models = ImmutableArray.CreateBuilder<PatchMapModel>();
                var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();

                foreach (var r in results)
                {
                    foreach (var d in r.Diagnostics)
                        diagnostics.Add(d);
                    if (r.Model is { } m)
                        models.Add(m);
                }

                var duplicateDiags = DetectDuplicates(models);
                foreach (var d in duplicateDiags)
                    diagnostics.Add(d);

                return (
                    Models: new EquatableArray<PatchMapModel>(models.ToImmutable()),
                    Diagnostics: new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()),
                    HasDuplicates: duplicateDiags.Length > 0);
            });

        context.RegisterSourceOutput(collected, static (spc, result) =>
        {
            foreach (var diag in result.Diagnostics)
                spc.ReportDiagnostic(diag.ToDiagnostic());

            if (result.HasDuplicates || result.Models.Length == 0)
                return;

            spc.AddSource("PatchApplier.g.cs", GeneratePatchApplier(result.Models));
            spc.AddSource("PatchlyServiceCollectionExtensions.g.cs", GenerateServiceCollectionExtensions(result.Models));
        });
    }

    private static bool IsCandidateClass(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax classDecl)
            return false;

        if (classDecl.BaseList == null)
            return false;

        foreach (var baseType in classDecl.BaseList.Types)
        {
            var typeName = baseType.Type.ToString();
            if (typeName.Contains("PatchMap"))
                return true;
        }

        return false;
    }

    private static (PatchMapModel? Model, EquatableArray<DiagnosticInfo> Diagnostics) TransformMap(
        GeneratorSyntaxContext ctx, System.Threading.CancellationToken ct)
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct);

        if (symbol == null)
            return (null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));

        var patchMapBase = FindPatchMapBase(symbol);
        if (patchMapBase == null)
            return (null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));

        if (symbol.IsAbstract)
            return (null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));

        if (symbol.TypeParameters.Length > 0)
            return (null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));

        var typeArgs = patchMapBase.TypeArguments;
        var patchType = typeArgs[0];
        var targetType = typeArgs[1];

        var model = new PatchMapModel(
            ClassName: symbol.Name,
            FullyQualifiedName: symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            PatchFullyQualifiedName: patchType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            TargetFullyQualifiedName: targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        return (model, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
    }

    private static INamedTypeSymbol? FindPatchMapBase(INamedTypeSymbol symbol)
    {
        var current = symbol.BaseType;
        while (current != null)
        {
            if (current.IsGenericType &&
                current.OriginalDefinition.ContainingNamespace.ToDisplayString() == "Patchly" &&
                current.OriginalDefinition.Name == "PatchMap" &&
                current.TypeArguments.Length == 2)
            {
                return current;
            }
            current = current.BaseType;
        }
        return null;
    }

    private static ImmutableArray<DiagnosticInfo> DetectDuplicates(ImmutableArray<PatchMapModel>.Builder models)
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var groups = new Dictionary<(string Patch, string Target), List<PatchMapModel>>();

        foreach (var model in models)
        {
            var key = (model.PatchFullyQualifiedName, model.TargetFullyQualifiedName);
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<PatchMapModel>();
                groups[key] = list;
            }
            list.Add(model);
        }

        foreach (var kvp in groups)
        {
            if (kvp.Value.Count <= 1)
                continue;

            var classNames = string.Join(", ", kvp.Value.ConvertAll(m => m.ClassName));
            var pairStr = $"({StripGlobalPrefix(kvp.Key.Patch)}, {StripGlobalPrefix(kvp.Key.Target)})";

            diagnostics.Add(DiagnosticInfo.Create(
                Diagnostics.DuplicatePatchMap,
                Location.None,
                classNames,
                pairStr));
        }

        return diagnostics.ToImmutable();
    }

    private static string StripGlobalPrefix(string fqn)
    {
        return fqn.StartsWith("global::") ? fqn.Substring("global::".Length) : fqn;
    }

    private static string GeneratePatchApplier(EquatableArray<PatchMapModel> models)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Patchly;");
        sb.AppendLine();
        sb.AppendLine($"[global::System.CodeDom.Compiler.GeneratedCode(\"Patchly.Generators\", \"{s_version}\")]");
        sb.AppendLine("internal sealed class PatchApplier : IPatchApplier");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly global::System.IServiceProvider _sp;");
        sb.AppendLine();
        sb.AppendLine("    public PatchApplier(global::System.IServiceProvider sp) => _sp = sp;");
        sb.AppendLine();
        sb.AppendLine("    public void Apply<TPatch, TTarget>(TPatch patch, TTarget target)");
        sb.AppendLine("        where TPatch : IPatchDocument");
        sb.AppendLine("    {");

        for (var i = 0; i < models.Length; i++)
        {
            var model = models[i];
            var elsePrefix = i == 0 ? "" : "else ";
            sb.AppendLine($"        {elsePrefix}if (typeof(TPatch) == typeof({model.PatchFullyQualifiedName}) && typeof(TTarget) == typeof({model.TargetFullyQualifiedName}))");
            sb.AppendLine("        {");
            sb.AppendLine($"            var map = (global::Patchly.PatchMap<{model.PatchFullyQualifiedName}, {model.TargetFullyQualifiedName}>)_sp.GetService(typeof(global::Patchly.PatchMap<{model.PatchFullyQualifiedName}, {model.TargetFullyQualifiedName}>))!;");
            sb.AppendLine($"            map.Apply(({model.PatchFullyQualifiedName})(object)patch!, ({model.TargetFullyQualifiedName})(object)target!);");
            sb.AppendLine("            return;");
            sb.AppendLine("        }");
        }

        sb.AppendLine();
        sb.AppendLine("        throw new global::System.InvalidOperationException($\"No PatchMap registered for {typeof(TPatch).FullName} -> {typeof(TTarget).FullName}\");");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateServiceCollectionExtensions(EquatableArray<PatchMapModel> models)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine($"[global::System.CodeDom.Compiler.GeneratedCode(\"Patchly.Generators\", \"{s_version}\")]");
        sb.AppendLine("internal static partial class PatchlyServiceCollectionExtensions");
        sb.AppendLine("{");

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers all discovered PatchMap classes and the <see cref=\"global::Patchly.IPatchApplier\"/> service.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine("    /// Registered maps:");
        for (var i = 0; i < models.Length; i++)
        {
            var model = models[i];
            sb.AppendLine($"    /// <list type=\"bullet\"><item><see cref=\"{StripGlobalPrefix(model.FullyQualifiedName)}\"/></item></list>");
        }
        sb.AppendLine("    /// </remarks>");

        sb.AppendLine("    public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddPatchlyMaps(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        services.AddScoped<global::Patchly.IPatchApplier, global::Patchly.PatchApplier>();");

        for (var i = 0; i < models.Length; i++)
        {
            var model = models[i];
            sb.AppendLine($"        services.AddTransient<global::Patchly.PatchMap<{model.PatchFullyQualifiedName}, {model.TargetFullyQualifiedName}>, {model.FullyQualifiedName}>();");
        }

        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
