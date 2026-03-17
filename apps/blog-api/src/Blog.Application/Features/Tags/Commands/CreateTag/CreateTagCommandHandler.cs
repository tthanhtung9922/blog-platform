using Blog.Application.Abstractions;
using Blog.Application.DTOs;
using Blog.Domain.Aggregates.Tags;
using Blog.Domain.Repositories;
using Blog.Domain.ValueObjects;
using MediatR;

namespace Blog.Application.Features.Tags.Commands.CreateTag;

public class CreateTagCommandHandler(
    ITagRepository tags,
    IUnitOfWork uow) : IRequestHandler<CreateTagCommand, TagDto>
{
    public async Task<TagDto> Handle(CreateTagCommand request, CancellationToken ct)
    {
        var slug = Slug.Create(request.Name);
        var tag = Tag.Create(request.Name, slug);

        await tags.AddAsync(tag, ct);

        // CommitAsync: SaveChangesAsync + dispatch TagCreatedEvent
        // TagCreatedCacheInvalidationHandler invalidates tag:list:* after this returns
        await uow.CommitAsync(ct);

        return new TagDto(tag.Id, tag.Name, tag.Slug.Value);
    }
}
