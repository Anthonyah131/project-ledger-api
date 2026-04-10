using Microsoft.AspNetCore.Http;
using ProjectLedger.API.DTOs.Expense;

namespace ProjectLedger.API.Services;

public interface IExpenseDocumentIntelligenceService
{
    /// <summary>
    /// Processes a document (PDF, Image) to extract expense or income draft details using AI.
    /// </summary>
    /// <param name="projectId">The project where the transaction belongs.</param>
    /// <param name="file">The uploaded document file.</param>
    /// <param name="documentKind">The type of document (e.g. "invoice", "receipt").</param>
    /// <param name="transactionKind">The transaction direction ("expense" or "income"). Defaults to "expense".</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ExtractExpenseFromDocumentResponse> ExtractDraftAsync(
        Guid projectId,
        IFormFile file,
        string documentKind,
    string transactionKind = "expense",
        CancellationToken ct = default);
}
