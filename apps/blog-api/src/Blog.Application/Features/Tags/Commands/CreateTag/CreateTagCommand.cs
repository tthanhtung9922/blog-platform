using Blog.Application.Abstractions;
using Blog.Application.DTOs;
using MediatR;

namespace Blog.Application.Features.Tags.Commands.CreateTag;

/// <summary>
/// Creates a new tag. Requires Admin or Editor role (IAuthorizedRequest).
/// Fires TagCreatedEvent after CommitAsync(), which invalidates tag:list:* cache.
/// </summary>
public record CreateTagCommand(string Name) : IRequest<TagDto>, IAuthorizedRequest
{
    public string[] RequiredRoles => new[] { "Admin", "Editor" };
}
