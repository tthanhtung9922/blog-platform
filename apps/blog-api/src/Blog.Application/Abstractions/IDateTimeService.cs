namespace Blog.Application.Abstractions;
public interface IDateTimeService
{
    DateTimeOffset UtcNow { get; }
}
