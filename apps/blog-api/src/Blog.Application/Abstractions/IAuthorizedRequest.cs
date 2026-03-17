namespace Blog.Application.Abstractions;
/// <summary>AuthorizationBehavior enforces role check for requests implementing this.</summary>
public interface IAuthorizedRequest
{
    string[] RequiredRoles { get; }
}
