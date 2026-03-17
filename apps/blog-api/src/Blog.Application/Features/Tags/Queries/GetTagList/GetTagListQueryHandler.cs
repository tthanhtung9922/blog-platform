using Blog.Application.DTOs;
using Blog.Domain.Repositories;
using MediatR;

namespace Blog.Application.Features.Tags.Queries.GetTagList;

public class GetTagListQueryHandler(ITagRepository tags)
    : IRequestHandler<GetTagListQuery, IReadOnlyList<TagDto>>
{
    public async Task<IReadOnlyList<TagDto>> Handle(
        GetTagListQuery request, CancellationToken ct)
    {
        var allTags = await tags.GetAllAsync(ct);
        return allTags
            .Select(t => new TagDto(t.Id, t.Name, t.Slug.Value))
            .ToList()
            .AsReadOnly();
    }
}
