using EduPlatform.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using EduPlatform.API.Hubs;

namespace EduPlatform.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly EduDbContext _db;
    private readonly IHubContext<NotificationsHub> _hub;

    public NotificationsController(EduDbContext db, IHubContext<NotificationsHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        var userId = GetUserId();
        return Ok(await _db.Notifications
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(50)
            .ToListAsync());
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = GetUserId();
        var notification = await _db.Notifications.FirstOrDefaultAsync(item =>
            item.Id == id && item.UserId == userId);
        if (notification == null)
            return NotFound();

        notification.IsRead = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("send")]
    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Send([FromBody] SendNotificationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Message))
            return BadRequest(new { message = "Le titre et le message sont obligatoires." });

        var senderId = GetUserId();
        if (!User.IsInRole("Admin"))
        {
            var isOwnStudent = await _db.Enrollments.AnyAsync(e =>
                e.UserId == dto.UserId && e.Course.InstructorId == senderId);
            if (!isOwnStudent)
                return Forbid();
        }

        var notification = new EduPlatform.Core.Models.Notification
        {
            UserId = dto.UserId,
            Title = dto.Title,
            Message = dto.Message
        };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();
        await _hub.Clients.User(notification.UserId.ToString()).SendAsync(
            "ReceiveNotification",
            notification);

        return Ok(notification);
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    }
}

public class SendNotificationDto
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
