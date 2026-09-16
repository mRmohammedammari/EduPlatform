using EduPlatform.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduPlatform.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly EduDbContext _db;

    public NotificationsController(EduDbContext db)
    {
        _db = db;
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

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    }
}
