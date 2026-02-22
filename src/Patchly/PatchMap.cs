namespace Patchly;

/// <summary>
/// Base class for a patch-to-entity mapping. Inherit and override <see cref="Apply"/>
/// with your mapping logic. Register all maps via the generated <c>AddPatchlyMaps()</c> extension method.
/// </summary>
public abstract class PatchMap<TPatch, TTarget> where TPatch : IPatchDocument
{
    /// <summary>Applies provided fields from <paramref name="patch"/> to <paramref name="target"/>.</summary>
    public abstract void Apply(TPatch patch, TTarget target);
}
