using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yusay.Api.Authorization;
using Yusay.Api.Common;
using Yusay.Api.Contracts.Tracking;
using Yusay.Application.Tracking.Queries.ListAdministrativeDimensions;

namespace Yusay.Api.Controllers.Administration;

[ApiController]
[Route("admin/dimensions")]
[Authorize(Policy = AdministratorPolicy.Name)]
[Tags("Yusay.Api")]
public sealed class DimensionAdminController(
    IListAdministrativeDimensionsUseCase listAdministrativeDimensionsUseCase) : ControllerBase
{
    private readonly IListAdministrativeDimensionsUseCase _listAdministrativeDimensionsUseCase =
        listAdministrativeDimensionsUseCase;

    [HttpGet]
    [EndpointName("ListAdministrativeDimensions")]
    [ProducesResponseType(typeof(ListDimensionsResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ListDimensionsResponse>> List(CancellationToken cancellationToken)
    {
        var items = await _listAdministrativeDimensionsUseCase.ExecuteAsync(cancellationToken);

        return Ok(DimensionContractMapper.ToListResponse(items));
    }
}
