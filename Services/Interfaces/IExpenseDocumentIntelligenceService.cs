using Microsoft.AspNetCore.Http;
using ProjectLedger.API.DTOs.Expense;

namespace ProjectLedger.API.Services;

public interface IExpenseDocumentIntelligenceService
{
    Task<ExtractExpenseFromDocumentResponse> ExtractDraftAsync(
        Guid projectId,
        IFormFile file,
        string documentKind,
    string transactionKind = "expense",
        CancellationToken ct = default);
}
