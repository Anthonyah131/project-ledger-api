using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Project;
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

    public ProjectPaymentMethodController(
        IProjectPaymentMethodService ppmService,
        IPlanAuthorizationService planAuth)
    {
        _ppmService = ppmService;
        _planAuth = planAuth;
    }

    /// <summary>Lista los métodos de pago vinculados a un proyecto.</summary>
    [HttpGet]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(IEnumerable<ProjectPaymentMethodResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLinkedPaymentMethods(Guid projectId, CancellationToken ct)
    {
        var links = await _ppmService.GetByProjectIdAsync(projectId, ct);
        return Ok(links.ToResponse());
    }

    /// <summary>Vincula un método de pago al proyecto (solo owner).</summary>
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

            // Recargar con includes para la respuesta
            var links = await _ppmService.GetByProjectIdAsync(projectId, ct);
            var full = links.FirstOrDefault(l => l.PpmId == created.PpmId);

            return CreatedAtAction(
                nameof(GetLinkedPaymentMethods),
                new { projectId },
                full?.ToResponse() ?? created.ToResponse());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Desvincula un método de pago del proyecto (solo owner).</summary>
    [HttpDelete("{paymentMethodId:guid}")]
    [Authorize(Policy = "ProjectOwner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkPaymentMethod(
        Guid projectId, Guid paymentMethodId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);

        try
        {
            await _ppmService.UnlinkAsync(projectId, paymentMethodId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
