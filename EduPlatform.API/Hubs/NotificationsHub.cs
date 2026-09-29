using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EduPlatform.API.Hubs;

[Authorize]
public sealed class NotificationsHub : Hub
{
}
