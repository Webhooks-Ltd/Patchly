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
    public string? Arg2 { get; }
    public string? Arg3 { get; }

    private DiagnosticInfo(DiagnosticDescriptor descriptor, string filePath, TextSpan textSpan, LinePositionSpan linePositionSpan, string arg0, string? arg1, string? arg2, string? arg3)
    {
        Descriptor = descriptor;
        FilePath = filePath;
        TextSpan = textSpan;
        LinePositionSpan = linePositionSpan;
        Arg0 = arg0;
        Arg1 = arg1;
        Arg2 = arg2;
        Arg3 = arg3;
    }

    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location location, string arg0, string? arg1 = null, string? arg2 = null, string? arg3 = null)
    {
        if (location.SourceTree != null)
        {
            return new DiagnosticInfo(
                descriptor,
                location.SourceTree.FilePath,
                location.SourceSpan,
                location.GetLineSpan().Span,
                arg0,
                arg1,
                arg2,
                arg3);
        }

        return new DiagnosticInfo(descriptor, "", default, default, arg0, arg1, arg2, arg3);
    }

    public Diagnostic ToDiagnostic()
    {
        var location = string.IsNullOrEmpty(FilePath)
            ? Location.None
            : Location.Create(FilePath, TextSpan, LinePositionSpan);

        if (Arg3 != null)
            return Diagnostic.Create(Descriptor, location, Arg0, Arg1, Arg2, Arg3);
        if (Arg2 != null)
            return Diagnostic.Create(Descriptor, location, Arg0, Arg1, Arg2);
        if (Arg1 != null)
            return Diagnostic.Create(Descriptor, location, Arg0, Arg1);
        return Diagnostic.Create(Descriptor, location, Arg0);
    }

    public bool Equals(DiagnosticInfo other) =>
        ReferenceEquals(Descriptor, other.Descriptor) &&
        FilePath == other.FilePath &&
        TextSpan.Equals(other.TextSpan) &&
        LinePositionSpan.Equals(other.LinePositionSpan) &&
        Arg0 == other.Arg0 &&
        Arg1 == other.Arg1 &&
        Arg2 == other.Arg2 &&
        Arg3 == other.Arg3;

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
            hash = hash * 31 + (Arg2?.GetHashCode() ?? 0);
            hash = hash * 31 + (Arg3?.GetHashCode() ?? 0);
            return hash;
        }
    }
}
