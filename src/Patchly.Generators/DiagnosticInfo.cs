using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Patchly.Generators;

internal readonly struct DiagnosticInfo : IEquatable<DiagnosticInfo>
{
    public DiagnosticDescriptor Descriptor { get; }
    public string FilePath { get; }
    public TextSpan TextSpan { get; }
    public LinePositionSpan LinePositionSpan { get; }
    public string Arg0 { get; }
    public string? Arg1 { get; }

    private DiagnosticInfo(DiagnosticDescriptor descriptor, string filePath, TextSpan textSpan, LinePositionSpan linePositionSpan, string arg0, string? arg1)
    {
        Descriptor = descriptor;
        FilePath = filePath;
        TextSpan = textSpan;
        LinePositionSpan = linePositionSpan;
        Arg0 = arg0;
        Arg1 = arg1;
    }

    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location location, string arg0, string? arg1 = null)
    {
        if (location.SourceTree != null)
        {
            return new DiagnosticInfo(
                descriptor,
                location.SourceTree.FilePath,
                location.SourceSpan,
                location.GetLineSpan().Span,
                arg0,
                arg1);
        }

        return new DiagnosticInfo(descriptor, "", default, default, arg0, arg1);
    }

    public Diagnostic ToDiagnostic()
    {
        var location = string.IsNullOrEmpty(FilePath)
            ? Location.None
            : Location.Create(FilePath, TextSpan, LinePositionSpan);

        return Arg1 != null
            ? Diagnostic.Create(Descriptor, location, Arg0, Arg1)
            : Diagnostic.Create(Descriptor, location, Arg0);
    }

    public bool Equals(DiagnosticInfo other) =>
        ReferenceEquals(Descriptor, other.Descriptor) &&
        FilePath == other.FilePath &&
        TextSpan.Equals(other.TextSpan) &&
        LinePositionSpan.Equals(other.LinePositionSpan) &&
        Arg0 == other.Arg0 &&
        Arg1 == other.Arg1;

    public override bool Equals(object? obj) => obj is DiagnosticInfo other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + (Descriptor?.GetHashCode() ?? 0);
            hash = hash * 31 + (FilePath?.GetHashCode() ?? 0);
            hash = hash * 31 + TextSpan.GetHashCode();
            hash = hash * 31 + Arg0.GetHashCode();
            hash = hash * 31 + (Arg1?.GetHashCode() ?? 0);
            return hash;
        }
    }
}
