using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using SakilaApp.Data;

namespace SakilaApp.Services;

public sealed class EmailQueueWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailQueueWorker> _logger;

    public EmailQueueWorker(IServiceScopeFactory scopeFactory, ILogger<EmailQueueWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "No fue posible procesar la cola de correos.");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    private async Task ProcessNextAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var now = DateTimeOffset.UtcNow;

        var item = await context.EmailQueue
            .Where(email => email.Status == "Pendiente" &&
                            email.ScheduledAt <= now &&
                            email.AttemptCount < email.MaxAttempts)
            .OrderBy(email => email.ScheduledAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (item is null) return;

        item.Status = "Procesando";
        item.AttemptCount++;
        item.LastAttemptAt = now;
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            await sender.SendEmailAsync(item.RecipientEmail, item.Subject, item.BodyHtml);
            item.Status = "Enviado";
            item.SentAt = DateTimeOffset.UtcNow;
            item.LastError = null;
        }
        catch (Exception exception)
        {
            item.Status = "Fallido";
            item.LastError = exception.Message.Length > 2000
                ? exception.Message[..2000]
                : exception.Message;
            _logger.LogWarning(exception, "Falló el envío del correo en cola {EmailQueueId}.", item.EmailQueueId);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
