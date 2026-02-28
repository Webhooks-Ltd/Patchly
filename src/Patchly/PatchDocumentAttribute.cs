namespace Patchly;

/// <summary>
/// Marks a partial class as a patch document. The source generator will emit a JSON converter,
/// field-tracking via <c>Provided</c>, and an <see cref="IPatchDocument"/> implementation.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PatchDocumentAttribute : Attribute
{
    /// <summary>
    /// Controls semantics generated for this patch document.
    /// </summary>
    public PatchSemanticsMode SemanticsMode { get; set; } = PatchSemanticsMode.Legacy;
}
