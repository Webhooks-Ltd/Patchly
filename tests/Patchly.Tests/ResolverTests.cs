using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Patchly.Generators;

namespace Patchly.Tests;

public class ResolverTests
{
    private static GeneratorDriverRunResult RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(PatchDocumentAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(JsonSerializer).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.IsExternalInit).Assembly.Location),
        };

        var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        var additionalRefs = new[]
        {
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "System.Collections.dll")),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "netstandard.dll")),
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references.Concat(additionalRefs),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new PatchDocumentGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        return driver.GetRunResult();
    }

    [Fact]
    public void Resolver_GeneratedWhenPatchDocumentTypesExist()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class CustomerPatch
            {
                public string? FirstName { get; set; }
            }
            """;

        var result = RunGenerator(source);

        result.GeneratedTrees.Should().Contain(t => t.FilePath.EndsWith("PatchlyJsonTypeInfoResolver.g.cs"));
    }

    [Fact]
    public void Resolver_NotGeneratedWhenNoPatchDocumentTypes()
    {
        const string source = """
            public class RegularClass
            {
                public string? Name { get; set; }
            }
            """;

        var result = RunGenerator(source);

        result.GeneratedTrees.Should().NotContain(t => t.FilePath.EndsWith("PatchlyJsonTypeInfoResolver.g.cs"));
    }

    [Fact]
    public void Resolver_ReturnsJsonTypeInfoForKnownTypes()
    {
        var resolver = PatchlyJsonTypeInfoResolver.Default;
        var options = new JsonSerializerOptions();

        var typeInfo = resolver.GetTypeInfo(typeof(CustomerPatch), options);

        typeInfo.Should().NotBeNull();
        typeInfo!.Type.Should().Be(typeof(CustomerPatch));
    }

    [Fact]
    public void Resolver_ReturnsNullForUnknownTypes()
    {
        var resolver = PatchlyJsonTypeInfoResolver.Default;
        var options = new JsonSerializerOptions();

        var typeInfo = resolver.GetTypeInfo(typeof(string), options);

        typeInfo.Should().BeNull();
    }

    [Fact]
    public void Resolver_HandlesNestedPatchDocumentTypes()
    {
        const string source = """
            using Patchly;

            public class Outer
            {
                [PatchDocument]
                public partial class InnerPatch
                {
                    public string? Name { get; set; }
                }
            }
            """;

        var result = RunGenerator(source);
        var resolverTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("PatchlyJsonTypeInfoResolver.g.cs"));

        resolverTree.Should().NotBeNull();
        var text = resolverTree!.GetText().ToString();
        text.Should().Contain("Outer.InnerPatch");
    }

    [Fact]
    public void Resolver_HandlesMultiplePatchDocumentTypes()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class CustomerPatch
            {
                public string? FirstName { get; set; }
            }

            [PatchDocument]
            public partial class OrderPatch
            {
                public string? OrderId { get; set; }
            }
            """;

        var result = RunGenerator(source);
        var resolverTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("PatchlyJsonTypeInfoResolver.g.cs"));

        resolverTree.Should().NotBeNull();
        var text = resolverTree!.GetText().ToString();
        text.Should().Contain("CustomerPatch");
        text.Should().Contain("OrderPatch");
    }
}
