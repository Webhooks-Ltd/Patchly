using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Patchly.Generators;

namespace Patchly.Tests;

public class DiagnosticTests
{
    private static ImmutableArray<Diagnostic> GetDiagnostics(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(PatchDocumentAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
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
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        return diagnostics;
    }

    private static void AssertDiagnostic(string source, string expectedId, DiagnosticSeverity expectedSeverity)
    {
        var diagnostics = GetDiagnostics(source);
        diagnostics.Should().Contain(d => d.Id == expectedId && d.Severity == expectedSeverity,
            $"expected diagnostic {expectedId} with severity {expectedSeverity}");
    }

    private static void AssertNoDiagnostics(string source)
    {
        var diagnostics = GetDiagnostics(source);
        var patchDiagnostics = diagnostics.Where(d => d.Id.StartsWith("PATCH")).ToArray();
        patchDiagnostics.Should().BeEmpty("expected no PATCH diagnostics but found: {0}",
            string.Join(", ", patchDiagnostics.Select(d => $"{d.Id}: {d.GetMessage()}")));
    }

    [Fact]
    public void PATCH001_NotPartialClass()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public class CustomerPatch
            {
                public string? FirstName { get; set; }
            }
            """;

        AssertDiagnostic(source, "PATCH001", DiagnosticSeverity.Error);
    }

    [Fact]
    public void PATCH002_AppliedToStruct()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial struct CustomerPatch
            {
                public string? FirstName { get; set; }
            }
            """;

        AssertDiagnostic(source, "PATCH002", DiagnosticSeverity.Error);
    }

    [Fact]
    public void PATCH003_AppliedToRecord()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial record CustomerPatch
            {
                public string? FirstName { get; set; }
            }
            """;

        AssertDiagnostic(source, "PATCH003", DiagnosticSeverity.Error);
    }

    [Fact]
    public void PATCH004_AppliedToAbstractClass()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public abstract partial class CustomerPatch
            {
                public string? FirstName { get; set; }
            }
            """;

        AssertDiagnostic(source, "PATCH004", DiagnosticSeverity.Error);
    }

    [Fact]
    public void PATCH005_AppliedToGenericClass()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class CustomerPatch<T>
            {
                public string? FirstName { get; set; }
            }
            """;

        AssertDiagnostic(source, "PATCH005", DiagnosticSeverity.Error);
    }

    [Fact]
    public void PATCH006_NoParameterlessConstructor()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class CustomerPatch
            {
                public CustomerPatch(string id) { }
                public string? FirstName { get; set; }
            }
            """;

        AssertDiagnostic(source, "PATCH006", DiagnosticSeverity.Error);
    }

    [Fact]
    public void PATCH010_NonNullableValueType()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class CustomerPatch
            {
                public int Count { get; set; }
            }
            """;

        AssertDiagnostic(source, "PATCH010", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void PATCH030_DeterministicMode_NonNullableCollection()
    {
        const string source = """
            using Patchly;
            using System.Collections.Generic;

            [PatchDocument(SemanticsMode = PatchSemanticsMode.DeterministicV1)]
            public partial class CustomerPatch
            {
                public List<string> Tags { get; set; } = new();
            }
            """;

        AssertDiagnostic(source, "PATCH030", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void PATCH030_LegacyMode_NoWarning()
    {
        const string source = """
            using Patchly;
            using System.Collections.Generic;

            [PatchDocument]
            public partial class CustomerPatch
            {
                public List<string> Tags { get; set; } = new();
            }
            """;

        var diagnostics = GetDiagnostics(source);
        diagnostics.Where(d => d.Id == "PATCH030").Should().BeEmpty();
    }

    [Fact]
    public void PATCH011_NoPublicProperties()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class CustomerPatch
            {
            }
            """;

        AssertDiagnostic(source, "PATCH011", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void PATCH012_ReadOnlyProperty()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class CustomerPatch
            {
                public string? Name { get; }
            }
            """;

        AssertDiagnostic(source, "PATCH012", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void InitOnlyProperty_NowSupported_EmitsBufferedPathInfo()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class CustomerPatch
            {
                public string? Name { get; init; }
            }
            """;

        AssertDiagnostic(source, "PATCH016", DiagnosticSeverity.Info);
    }

    [Fact]
    public void PATCH014_JsonExtensionData()
    {
        const string source = """
            using Patchly;
            using System.Text.Json;
            using System.Text.Json.Serialization;
            using System.Collections.Generic;

            [PatchDocument]
            public partial class CustomerPatch
            {
                [JsonExtensionData]
                public Dictionary<string, JsonElement>? Extensions { get; set; }
            }
            """;

        AssertDiagnostic(source, "PATCH014", DiagnosticSeverity.Error);
    }

    [Fact]
    public void JsonConstructor_Parameterless_NowSupported_EmitsBufferedPathInfo()
    {
        const string source = """
            using Patchly;
            using System.Text.Json.Serialization;

            [PatchDocument]
            public partial class CustomerPatch
            {
                [JsonConstructor]
                public CustomerPatch() { }
                public string? FirstName { get; set; }
            }
            """;

        AssertDiagnostic(source, "PATCH016", DiagnosticSeverity.Info);
    }

    [Fact]
    public void PATCH016_BufferedPath_InitProperty()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class CustomerPatch
            {
                public string? Name { get; init; }
            }
            """;

        AssertDiagnostic(source, "PATCH016", DiagnosticSeverity.Info);
    }

    [Fact]
    public void PATCH016_BufferedPath_JsonConstructor()
    {
        const string source = """
            using Patchly;
            using System.Text.Json.Serialization;

            [PatchDocument]
            public partial class CustomerPatch
            {
                [JsonConstructor]
                public CustomerPatch(string? name) { Name = name; }
                public string? Name { get; set; }
            }
            """;

        AssertDiagnostic(source, "PATCH016", DiagnosticSeverity.Info);
    }

    [Fact]
    public void PATCH017_UnmatchedConstructorParameter()
    {
        const string source = """
            using Patchly;
            using System.Text.Json.Serialization;

            [PatchDocument]
            public partial class CustomerPatch
            {
                [JsonConstructor]
                public CustomerPatch(string? name, string? role = "user") { Name = name; }
                public string? Name { get; set; }
            }
            """;

        AssertDiagnostic(source, "PATCH017", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void PATCH017_UnmatchedConstructorParameter_NoDefault()
    {
        const string source = """
            using Patchly;
            using System.Text.Json.Serialization;

            [PatchDocument]
            public partial class CustomerPatch
            {
                [JsonConstructor]
                public CustomerPatch(string? name, string? middleName) { Name = name; }
                public string? Name { get; set; }
            }
            """;

        AssertDiagnostic(source, "PATCH017", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void PATCH018_MultipleJsonConstructors()
    {
        const string source = """
            using Patchly;
            using System.Text.Json.Serialization;

            [PatchDocument]
            public partial class CustomerPatch
            {
                [JsonConstructor]
                public CustomerPatch() { }
                [JsonConstructor]
                public CustomerPatch(string? name) { }
                public string? Name { get; set; }
            }
            """;

        AssertDiagnostic(source, "PATCH018", DiagnosticSeverity.Error);
    }

    [Fact]
    public void PATCH019_InitOnlyPropertyNotCoveredByConstructor()
    {
        const string source = """
            using Patchly;
            using System.Text.Json.Serialization;

            [PatchDocument]
            public partial class CustomerPatch
            {
                [JsonConstructor]
                public CustomerPatch(string? name) { Name = name; }
                public string? Name { get; init; }
                public int? Age { get; init; }
            }
            """;

        AssertDiagnostic(source, "PATCH019", DiagnosticSeverity.Error);
    }

    [Fact]
    public void PATCH021_ConstructorParameterTypeMismatch()
    {
        const string source = """
            using Patchly;
            using System.Text.Json.Serialization;

            [PatchDocument]
            public partial class CustomerPatch
            {
                [JsonConstructor]
                public CustomerPatch(int? name) { }
                public string? Name { get; set; }
            }
            """;

        AssertDiagnostic(source, "PATCH021", DiagnosticSeverity.Error);
    }

    [Fact]
    public void PATCH022_JsonConstructorMissingSetsRequiredMembers()
    {
        const string source = """
            using Patchly;
            using System.Text.Json.Serialization;

            [PatchDocument]
            public partial class CustomerPatch
            {
                [JsonConstructor]
                public CustomerPatch(string? name) { Name = name; }
                public required string? Name { get; set; }
            }
            """;

        AssertDiagnostic(source, "PATCH022", DiagnosticSeverity.Error);
    }

    [Fact]
    public void PATCH006_UpdatedMessage_NoParameterlessOrJsonConstructor()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class CustomerPatch
            {
                public CustomerPatch(string id) { }
                public string? FirstName { get; set; }
            }
            """;

        AssertDiagnostic(source, "PATCH006", DiagnosticSeverity.Error);
    }

    [Fact]
    public void JsonConstructor_OnlyParamCtor_NoError()
    {
        const string source = """
            using Patchly;
            using System.Text.Json.Serialization;

            [PatchDocument]
            public partial class CustomerPatch
            {
                [JsonConstructor]
                public CustomerPatch(string? name) { Name = name; }
                public string? Name { get; set; }
            }
            """;

        var diagnostics = GetDiagnostics(source);
        diagnostics.Where(d => d.Id == "PATCH006").Should().BeEmpty();
    }

    [Fact]
    public void Generator_HandlesErrorTypes_WithoutCrashing()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class CustomerPatch
            {
                public UnresolvableType? Data { get; set; }
                public string? Name { get; set; }
            }
            """;

        var diagnostics = GetDiagnostics(source);
        var patchDiagnostics = diagnostics.Where(d => d.Id.StartsWith("PATCH")).ToArray();
        patchDiagnostics.Should().NotContain(d => d.Id == "PATCH099",
            "generator should handle error types gracefully without crashing");
    }

    [Fact]
    public void ValidClass_NoPatchDiagnostics()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class CustomerPatch
            {
                public string? FirstName { get; set; }
                public string? LastName { get; set; }
                public int? Age { get; set; }
            }
            """;

        AssertNoDiagnostics(source);
    }

    [Fact]
    public void ClassWithBothParameterlessAndParameterized_NoError()
    {
        const string source = """
            using Patchly;

            [PatchDocument]
            public partial class CustomerPatch
            {
                public CustomerPatch() { }
                public CustomerPatch(string id) { }
                public string? FirstName { get; set; }
            }
            """;

        AssertNoDiagnostics(source);
    }
}
