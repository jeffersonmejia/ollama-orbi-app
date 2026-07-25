using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using MimeKit;
using SakilaApp.Settings;

namespace SakilaApp.Services;

public class GmailEmailSender : IEmailSender<IdentityUser>, IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly ILogger<GmailEmailSender> _logger;

    public GmailEmailSender(IOptions<EmailSettings> settings, ILogger<GmailEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        try
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(
                _settings.SenderName,
                _settings.SenderEmail));

            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;

            message.Body = new BodyBuilder
            {
                HtmlBody = htmlMessage
            }.ToMessageBody();

            using var client = new SmtpClient();

            await client.ConnectAsync(
                _settings.SmtpServer,
                _settings.SmtpPort,
                SecureSocketOptions.StartTls);

            await client.AuthenticateAsync(
                _settings.SenderEmail,
                _settings.Password);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar correo a {Email} con asunto '{Subject}'.", email, subject);
        }
    }

    public async Task SendConfirmationLinkAsync(IdentityUser user, string email, string confirmationLink)
    {
        await SendEmailAsync(email, "Confirma tu cuenta",
            $"Por favor confirma tu cuenta haciendo clic <a href='{confirmationLink}'>aquí</a>.");
    }

    public async Task SendPasswordResetLinkAsync(IdentityUser user, string email, string resetLink)
    {
        await SendEmailAsync(email, "Restablece tu contraseña",
            $"Por favor restablece tu contraseña haciendo clic <a href='{resetLink}'>aquí</a>.");
    }

    public async Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode)
    {
        await SendEmailAsync(email, "Código de restablecimiento de contraseña",
            $"Tu código de restablecimiento es: {resetCode}");
    }
}