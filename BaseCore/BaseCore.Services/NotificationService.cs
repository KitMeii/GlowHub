using BaseCore.Entities;
using BaseCore.Repository;

namespace BaseCore.Services
{
    public class NotificationService
    {
        private readonly MySqlDbContext _db;

        public NotificationService(MySqlDbContext db)
        {
            _db = db;
        }

        public async Task CreateAsync(string userId, NotificationType type, string title, string message, string? link = null)
        {
            var notification = new Notification
            {
                UserId    = userId,
                Type      = type,
                Title     = title,
                Message   = message,
                Link      = link,
                CreatedAt = DateTime.UtcNow
            };
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();
        }
    }
}
