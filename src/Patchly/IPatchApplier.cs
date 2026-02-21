namespace Patchly;

/// <summary>
/// Applies a registered <see cref="PatchMap{TPatch, TTarget}"/> to mutate a target entity.
/// Inject this service to apply any patch type without depending on individual map classes.
/// </summary>
public interface IPatchApplier
{
    /// <summary>
    /// Applies the registered map for the <typeparamref name="TPatch"/>/<typeparamref name="TTarget"/> pair.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">No map is registered for the pair.</exception>
    void Apply<TPatch, TTarget>(TPatch patch, TTarget target)
        where TPatch : IPatchDocument;
}
