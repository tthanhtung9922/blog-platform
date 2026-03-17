using Blog.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Blog.Infrastructure.Services.NoOp;

/// <summary>
/// Placeholder email service for Phase 2. Phase 3 replaces with Postal SMTP implementation.
/// Logs a debug message so developers can see email intent without actual delivery.
/// </summary>
public class NoOpEmailService(ILogger<NoOpEmailService> logger) : IEmailService
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogDebug(
            "NoOpEmailService: would send email to {ToEmail} with subject '{Subject}'",
            toEmail, subject);
        return Task.CompletedTask;
    }
}
