using System.Text.Json;
using Devices.Application;
using Devices.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartLocks.Application;

namespace SmartLocks.Infrastructure;

/// <summary>Retries a persisted command only until its short server-issued expiry.</summary>
public sealed class LockCommandOutboxDispatcher(IServiceScopeFactory scopeFactory, ILogger<LockCommandOutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await DispatchBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Lock command outbox dispatch failed."); }
        }
    }

    private async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var commands = scope.ServiceProvider.GetRequiredService<ILockCommandRepository>();
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
        var mqtt = scope.ServiceProvider.GetRequiredService<IMqttPublisher>();
        foreach (var command in await commands.GetQueuedForDispatchAsync(25, cancellationToken))
        {
            var device = await devices.GetByIdAsync(command.DeviceId, cancellationToken);
            if (device?.Status != DeviceStatus.Online)
                continue;

            var payload = JsonSerializer.Serialize(new
            {
                command = "unlock",
                lockId = command.LockId,
                commandId = command.Id,
                requestedAtUtc = command.RequestedAtUtc,
                expiresAtUtc = command.ExpiresAtUtc,
                expiresAtUnixTimeSeconds = new DateTimeOffset(command.ExpiresAtUtc).ToUnixTimeSeconds()
            });
            try
            {
                await mqtt.PublishAsync($"{device.MqttTopic}/commands", payload);
                await commands.MarkPublishedAsync(command.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Controller command {CommandId} remains queued for retry until expiry.", command.Id);
            }
        }
    }
}
