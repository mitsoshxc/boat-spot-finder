using BoatSpotFinder.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BoatSpotFinder.Infrastructure.Email;

public class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string htmlBody)
    {
        _logger.LogInformation("Email to={To} subject={Subject} body={Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}
