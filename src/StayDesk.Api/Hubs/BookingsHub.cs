using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StayDesk.Application.Auth;

namespace StayDesk.Api.Hubs;

// CAL-2, SRS §6 WS /hubs/bookings. Staff (and Admin, who holds Staff) choose what to watch:
// one property's calendar or the dashboard. SignalR forgets group membership when a
// connection drops, so clients watch again after reconnecting.
[Authorize(Roles = Roles.Staff)]
public sealed class BookingsHub : Hub
{
    public const string Path = "/hubs/bookings";
    public const string DashboardGroup = "dashboard";

    public static string PropertyGroup(Guid propertyId) => $"property-{propertyId}";

    public Task WatchProperty(Guid propertyId) => Groups.AddToGroupAsync(Context.ConnectionId, PropertyGroup(propertyId));

    public Task UnwatchProperty(Guid propertyId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, PropertyGroup(propertyId));

    public Task WatchDashboard() => Groups.AddToGroupAsync(Context.ConnectionId, DashboardGroup);

    public Task UnwatchDashboard() => Groups.RemoveFromGroupAsync(Context.ConnectionId, DashboardGroup);
}
