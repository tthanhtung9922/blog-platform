using Blog.Application.Abstractions;
using Blog.Application.Common.Exceptions;
using MediatR;
namespace Blog.Application.Behaviors;

public class AuthorizationBehavior<TRequest, TResponse>(
    ICurrentUserService currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        // Public requests bypass authorization entirely
        if (request is IAllowAnonymous) return await next();

        // Authorized requests: verify role membership
        if (request is IAuthorizedRequest authorized)
        {
            var userRole = currentUser.Role
                ?? throw new ForbiddenAccessException();

            if (!authorized.RequiredRoles.Contains(userRole))
                throw new ForbiddenAccessException();
        }

        return await next();
    }
}
