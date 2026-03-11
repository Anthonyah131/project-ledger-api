namespace ProjectLedger.API.Services;

public interface ITransactionReferenceGuardService
{
    Task EnsureCategoryCanBeDeletedAsync(Guid categoryId, CancellationToken ct = default);
    Task EnsurePaymentMethodCanBeDeletedAsync(Guid paymentMethodId, CancellationToken ct = default);
    Task EnsureAlternativeCurrencyCanBeRemovedAsync(Guid projectId, string currencyCode, CancellationToken ct = default);
    Task EnsureProjectPaymentMethodCanBeUnlinkedAsync(Guid projectId, Guid paymentMethodId, CancellationToken ct = default);
}