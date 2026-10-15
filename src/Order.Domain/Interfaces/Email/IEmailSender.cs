using Order.Domain.Interfaces.Email.Models;

namespace Order.Domain.Interfaces.Email
{
    public interface IEmailSender
    {
        Task SendAsync(EmailMessage emailMessage, CancellationToken cancellationToken = default);
    }
}