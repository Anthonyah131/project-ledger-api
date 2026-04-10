using Microsoft.Extensions.Localization;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Project;
using ProjectLedger.API.DTOs.ProjectPartner;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Models;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/payment-methods")]
[Authorize]
[Tags("Project Payment Methods")]
[Produces("application/json")]
public class ProjectPaymentMethodController : ControllerBase
{
    private readonly IProjectPaymentMethodService _ppmService;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IProjectPartnerService _projectPartnerService;
    private readonly IProjectAccessService _accessService;
    private readonly IStringLocalizer<Messages> _localizer;

    public ProjectPaymentMethodController(
        IProjectPaymentMethodService ppmService,
        IPlanAuthorizationService planAuth,
        IProjectPartnerService projectPartnerService,
        IProjectAccessService accessService,
        IStringLocalizer<Messages> localizer)
    {
        _ppmService = ppmService;
        _planAuth = planAuth;
        _projectPartnerService = projectPartnerService;
        _accessService = accessService;
        _localizer = localizer;
    }

    /// <summary>
    /// Payment methods that can be linked to the project: they belong to an assigned partner
    /// in the project and are not yet linked to it.
    /// It does not include unpartnered methods or methods from unassigned partners.
    /// </summary>
    [HttpGet("linkable")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(IEnumerable<LinkablePaymentMethodResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLinkablePaymentMethods(Guid projectId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Viewer, ct);

        var paymentMethods = await _projectPartnerService.GetLinkablePaymentMethodsAsync(projectId, userId, ct);

        var result = paymentMethods.Select(pm => new LinkablePaymentMethodResponse
        {
            Id = pm.PmtId,
            Name = pm.PmtName,
            Type = pm.PmtType,
            Currency = pm.PmtCurrency,
            BankName = pm.PmtBankName,
            AccountNumber = pm.PmtAccountNumber,
            PartnerId = pm.PmtOwnerPartnerId!.Value,
            PartnerName = pm.OwnerPartner?.PtrName ?? string.Empty
        });

        return Ok(result);
    }

    /// <summary>Lists the payment methods linked to a project.</summary>
    [HttpGet]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(IEnumerable<ProjectPaymentMethodResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLinkedPaymentMethods(Guid projectId, CancellationToken ct)
    {
        var links = await _ppmService.GetByProjectIdAsync(projectId, ct);
        return Ok(links.ToResponse());
    }

    /// <summary>Links a payment method to the project (owner only).</summary>
    [HttpPost]
    [Authorize(Policy = "ProjectOwner")]
    [ProducesResponseType(typeof(ProjectPaymentMethodResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkPaymentMethod(
        Guid projectId,
        [FromBody] LinkPaymentMethodRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);

        var link = new ProjectPaymentMethod
        {
            PpmId = Guid.NewGuid(),
            PpmProjectId = projectId,
            PpmPaymentMethodId = request.PaymentMethodId,
            PpmAddedByUserId = userId
        };

        try
        {
            var created = await _ppmService.LinkAsync(link, ct);

            // Reload with includes for the response
            var links = await _ppmService.GetByProjectIdAsync(projectId, ct);
            var full = links.FirstOrDefault(l => l.PpmId == created.PpmId);

            return CreatedAtAction(
                nameof(GetLinkedPaymentMethods),
                new { projectId },
                full?.ToResponse() ?? created.ToResponse());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer[ex.Message]));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(LocalizedResponse.Create("CONFLICT", _localizer[ex.Message]));
        }
    }

    /// <summary>Unlinks a payment method from the project (owner only). Uses the link's <c>id</c> returned by GET.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ProjectOwner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkPaymentMethod(
        Guid projectId, Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);

        try
        {
            await _ppmService.UnlinkAsync(projectId, id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer[ex.Message]));
        }
    }
}
