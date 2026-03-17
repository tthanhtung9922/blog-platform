using Blog.Application.Abstractions;

namespace Blog.Infrastructure.Services;

/// <summary>
/// Returns the real system clock UTC time.
/// Wrapping DateTimeOffset.UtcNow behind an interface allows unit tests
/// to inject a fake clock without patching static state.
/// </summary>
public class DateTimeService : IDateTimeService
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
