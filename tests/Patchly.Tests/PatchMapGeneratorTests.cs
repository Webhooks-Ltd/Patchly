using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Patchly.Generators;

namespace Patchly.Tests;

public class PatchMapGeneratorTests
{
    private static (GeneratorDriverRunResult Result, ImmutableArray<Diagnostic> Diagnostics) RunGenerators(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(PatchDocumentAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.IsExternalInit).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.IServiceProvider).Assembly.Location),
        };

        var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        var additionalRefs = new[]
        {
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "System.Collections.dll")),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "System.ComponentModel.dll")),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "netstandard.dll")),
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references.Concat(additionalRefs),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new PatchDocumentGenerator(),
            new PatchMapGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var compilationDiags = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        return (driver.GetRunResult(), diagnostics.AddRange(compilationDiags));
    }

    private static string GetGeneratedSource(GeneratorDriverRunResult result, string fileName)
    {
        var tree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith(fileName));
        return tree?.GetText().ToString() ?? "";
    }

    [Fact]
    public void SingleMap_GeneratesPatchApplierAndExtensions()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class OrderPatch
            {
                public string? OrderId { get; set; }
            }

            public class Order
            {
                public string? OrderId { get; set; }
            }

            public class OrderPatchMap : PatchMap<OrderPatch, Order>
            {
                public override void Apply(OrderPatch patch, Order target)
                {
                    if (patch.Provided.OrderId) target.OrderId = patch.OrderId;
                }
            }
            """;

        var (result, diagnostics) = RunGenerators(source);
        var patchDiags = diagnostics.Where(d => d.Id.StartsWith("PATCH")).ToArray();
        patchDiags.Should().BeEmpty();

        var applierSource = GetGeneratedSource(result, "PatchApplier.g.cs");
        applierSource.Should().Contain("internal sealed class PatchApplier");
        applierSource.Should().Contain(": IPatchApplier");
        applierSource.Should().Contain("typeof(global::OrderPatch)");
        applierSource.Should().Contain("typeof(global::Order)");

        var extSource = GetGeneratedSource(result, "PatchlyServiceCollectionExtensions.g.cs");
        extSource.Should().Contain("AddPatchlyMaps");
        extSource.Should().Contain("AddScoped");
        extSource.Should().Contain("AddTransient");
        extSource.Should().Contain("OrderPatchMap");
    }

    [Fact]
    public void MultipleMaps_DifferentNamespaces_AllRegistered()
    {
        const string source = """
            using Patchly;

            namespace App.Patches
            {
                [PatchDocument]
                public partial class CustomerPatch
                {
                    public string? Name { get; set; }
                }
            }

            namespace App.Patches
            {
                [PatchDocument]
                public partial class OrderPatch
                {
                    public string? OrderId { get; set; }
                }
            }

            namespace App.Domain
            {
                public class Customer { public string? Name { get; set; } }
                public class Order { public string? OrderId { get; set; } }
            }

            namespace App.Maps
            {
                public class CustomerPatchMap : PatchMap<App.Patches.CustomerPatch, App.Domain.Customer>
                {
                    public override void Apply(App.Patches.CustomerPatch patch, App.Domain.Customer target)
                    {
                        if (patch.Provided.Name) target.Name = patch.Name;
                    }
                }
            }

            namespace App.Maps
            {
                public class OrderPatchMap : PatchMap<App.Patches.OrderPatch, App.Domain.Order>
                {
                    public override void Apply(App.Patches.OrderPatch patch, App.Domain.Order target)
                    {
                        if (patch.Provided.OrderId) target.OrderId = patch.OrderId;
                    }
                }
            }
            """;

        var (result, diagnostics) = RunGenerators(source);
        var patchDiags = diagnostics.Where(d => d.Id.StartsWith("PATCH")).ToArray();
        patchDiags.Should().BeEmpty();

        var applierSource = GetGeneratedSource(result, "PatchApplier.g.cs");
        applierSource.Should().Contain("App.Patches.CustomerPatch");
        applierSource.Should().Contain("App.Domain.Customer");
        applierSource.Should().Contain("App.Patches.OrderPatch");
        applierSource.Should().Contain("App.Domain.Order");

        var extSource = GetGeneratedSource(result, "PatchlyServiceCollectionExtensions.g.cs");
        extSource.Should().Contain("CustomerPatchMap");
        extSource.Should().Contain("OrderPatchMap");
    }

    [Fact]
    public void AbstractIntermediateClass_Skipped_ConcreteSubclassRegistered()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class CustomerPatch
            {
                public string? Name { get; set; }
            }

            public class Customer { public string? Name { get; set; } }

            public abstract class AuditedPatchMap<TPatch, TTarget> : PatchMap<TPatch, TTarget>
                where TPatch : IPatchDocument
            {
            }

            public class CustomerPatchMap : AuditedPatchMap<CustomerPatch, Customer>
            {
                public override void Apply(CustomerPatch patch, Customer target)
                {
                    if (patch.Provided.Name) target.Name = patch.Name;
                }
            }
            """;

        var (result, diagnostics) = RunGenerators(source);
        var patchDiags = diagnostics.Where(d => d.Id.StartsWith("PATCH")).ToArray();
        patchDiags.Should().BeEmpty();

        var extSource = GetGeneratedSource(result, "PatchlyServiceCollectionExtensions.g.cs");
        extSource.Should().Contain("CustomerPatchMap");
        extSource.Should().NotContain("AuditedPatchMap");
    }

    [Fact]
    public void OpenGenericSubclass_Skipped()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class SomePatch
            {
                public string? Name { get; set; }
            }

            public class GenericMap<T> : PatchMap<SomePatch, T>
            {
                public override void Apply(SomePatch patch, T target) { }
            }
            """;

        var (result, _) = RunGenerators(source);

        result.GeneratedTrees.Should().NotContain(t => t.FilePath.EndsWith("PatchApplier.g.cs"));
        result.GeneratedTrees.Should().NotContain(t => t.FilePath.EndsWith("PatchlyServiceCollectionExtensions.g.cs"));
    }

    [Fact]
    public void NestedClassMap_UsesFullyQualifiedName()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class CustomerPatch
            {
                public string? Name { get; set; }
            }

            public class Customer { public string? Name { get; set; } }

            public class Outer
            {
                public class CustomerPatchMap : PatchMap<CustomerPatch, Customer>
                {
                    public override void Apply(CustomerPatch patch, Customer target)
                    {
                        if (patch.Provided.Name) target.Name = patch.Name;
                    }
                }
            }
            """;

        var (result, diagnostics) = RunGenerators(source);
        var patchDiags = diagnostics.Where(d => d.Id.StartsWith("PATCH")).ToArray();
        patchDiags.Should().BeEmpty();

        var extSource = GetGeneratedSource(result, "PatchlyServiceCollectionExtensions.g.cs");
        extSource.Should().Contain("Outer.CustomerPatchMap");
    }

    [Fact]
    public void InternalMapClass_DiscoveredAndRegistered()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class CustomerPatch
            {
                public string? Name { get; set; }
            }

            public class Customer { public string? Name { get; set; } }

            internal class CustomerPatchMap : PatchMap<CustomerPatch, Customer>
            {
                public override void Apply(CustomerPatch patch, Customer target)
                {
                    if (patch.Provided.Name) target.Name = patch.Name;
                }
            }
            """;

        var (result, diagnostics) = RunGenerators(source);
        var patchDiags = diagnostics.Where(d => d.Id.StartsWith("PATCH")).ToArray();
        patchDiags.Should().BeEmpty();

        var extSource = GetGeneratedSource(result, "PatchlyServiceCollectionExtensions.g.cs");
        extSource.Should().Contain("CustomerPatchMap");
    }

    [Fact]
    public void DuplicatePatchTargetPair_EmitsPATCH020()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class CustomerPatch
            {
                public string? Name { get; set; }
            }

            public class Customer { public string? Name { get; set; } }

            public class CustomerPatchMapA : PatchMap<CustomerPatch, Customer>
            {
                public override void Apply(CustomerPatch patch, Customer target)
                {
                    if (patch.Provided.Name) target.Name = patch.Name;
                }
            }

            public class CustomerPatchMapB : PatchMap<CustomerPatch, Customer>
            {
                public override void Apply(CustomerPatch patch, Customer target)
                {
                    if (patch.Provided.Name) target.Name = patch.Name;
                }
            }
            """;

        var (_, diagnostics) = RunGenerators(source);

        diagnostics.Should().Contain(d => d.Id == "PATCH020" && d.Severity == DiagnosticSeverity.Error);
        var diag = diagnostics.First(d => d.Id == "PATCH020");
        var msg = diag.GetMessage();
        msg.Should().Contain("CustomerPatchMapA");
        msg.Should().Contain("CustomerPatchMapB");
        msg.Should().Contain("CustomerPatch");
        msg.Should().Contain("Customer");
    }

    [Fact]
    public void ZeroMaps_NoGeneratedOutput()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class CustomerPatch
            {
                public string? Name { get; set; }
            }
            """;

        var (result, _) = RunGenerators(source);

        result.GeneratedTrees.Should().NotContain(t => t.FilePath.EndsWith("PatchApplier.g.cs"));
        result.GeneratedTrees.Should().NotContain(t => t.FilePath.EndsWith("PatchlyServiceCollectionExtensions.g.cs"));
    }

    [Fact]
    public void ConstraintViolation_FailsToCompile()
    {
        var source = """
            using Patchly;

            public class NotAPatch { }
            public class Target { }

            public class BadMap : PatchMap<NotAPatch, Target>
            {
                public override void Apply(NotAPatch patch, Target target) { }
            }
            """;

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(PatchDocumentAttribute).Assembly.Location),
        };
        var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        var additionalRefs = new[]
        {
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "netstandard.dll")),
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references.Concat(additionalRefs),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        errors.Should().Contain(d => d.Id == "CS0311",
            "PatchMap<string, T> should fail to compile due to constraint violation on TPatch");
    }

    [Fact]
    public void ThreeNamespaces_FullyQualifiedNamesCompile()
    {
        const string source = """
            using Patchly;

            namespace MyApp.Patches
            {
                [PatchDocument]
                public partial class CustomerPatch
                {
                    public string? Name { get; set; }
                }
            }

            namespace MyApp.Domain
            {
                public class Customer { public string? Name { get; set; } }
            }

            namespace MyApp.Maps
            {
                public class CustomerPatchMap : PatchMap<MyApp.Patches.CustomerPatch, MyApp.Domain.Customer>
                {
                    public override void Apply(MyApp.Patches.CustomerPatch patch, MyApp.Domain.Customer target)
                    {
                        if (patch.Provided.Name) target.Name = patch.Name;
                    }
                }
            }
            """;

        var (result, diagnostics) = RunGenerators(source);
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        errors.Should().BeEmpty();

        var applierSource = GetGeneratedSource(result, "PatchApplier.g.cs");
        applierSource.Should().Contain("MyApp.Patches.CustomerPatch");
        applierSource.Should().Contain("MyApp.Domain.Customer");

        var extSource = GetGeneratedSource(result, "PatchlyServiceCollectionExtensions.g.cs");
        extSource.Should().Contain("MyApp.Maps.CustomerPatchMap");
    }
}
