using System.Security.Claims;
using Homes.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ApiHost;

[Authorize]
public sealed class MonitoringHub(IHomeRepository homes) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            Context.Abort();
            return;
        }

        if (string.Equals(Context.User?.FindFirstValue(ClaimTypes.Role), "SystemAdmin", StringComparison.Ordinal))
            await Groups.AddToGroupAsync(Context.ConnectionId, "system-admin");

        foreach (var home in await homes.GetForUserAsync(userId, Context.ConnectionAborted))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"home:{home.Id:N}");

        await base.OnConnectedAsync();
    }
}
