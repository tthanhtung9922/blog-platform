using Blog.Application.Features.Tags.Commands.CreateTag;
using Blog.Application.Features.Tags.Queries.GetTagList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Blog.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class TagsController(IMediator mediator) : ControllerBase
{
    /// <summary>Returns all tags. Public endpoint — no authentication required.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await mediator.Send(new GetTagListQuery(), ct);
        return Ok(result);
    }

    /// <summary>Creates a new tag. Requires Admin or Editor role.</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateTagCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetAll), result);
    }
}
