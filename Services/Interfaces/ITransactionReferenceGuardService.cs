namespace ProjectLedger.API.Services;

public interface ITransactionReferenceGuardService
{
    /// <summary>
    /// Ensures that a category can be safely deleted without breaking transaction integrity.
    /// </summary>
    Task EnsureCategoryCanBeDeletedAsync(Guid categoryId, CancellationToken ct = default);

    /// <summary>
    /// Ensures that a payment method can be safely deleted.
    /// </summary>
    Task EnsurePaymentMethodCanBeDeletedAsync(Guid paymentMethodId, CancellationToken ct = default);

    /// <summary>
    /// Validates if an alternative currency can be removed from a project's configuration.
    /// </summary>
    Task EnsureAlternativeCurrencyCanBeRemovedAsync(Guid projectId, string currencyCode, CancellationToken ct = default);

    /// <summary>
    /// Checks if a payment method can be unlinked from a project.
    /// </summary>
    Task EnsureProjectPaymentMethodCanBeUnlinkedAsync(Guid projectId, Guid paymentMethodId, CancellationToken ct = default);

    /// <summary>
    /// Validates if an obligation can be deleted based on its payment status.
    /// </summary>
    Task EnsureObligationCanBeDeletedAsync(Guid obligationId, CancellationToken ct = default);
}