using Blog.Application.Abstractions;
using Blog.Application.DTOs;
using MediatR;

namespace Blog.Application.Features.Tags.Queries.GetTagList;

/// <summary>
/// Returns all tags ordered by name (flat list, no pagination — tags are a small fixed set).
/// ICacheableQuery: CachingBehavior serves from Redis on second call (TTL 1 hour).
/// IAllowAnonymous: AuthorizationBehavior skips role check — tag list is a public endpoint.
/// </summary>
public record GetTagListQuery : IRequest<IReadOnlyList<TagDto>>, ICacheableQuery, IAllowAnonymous
{
    public string CacheKey => "tag:list:all";
    public TimeSpan? CacheDuration => TimeSpan.FromHours(1);
}
