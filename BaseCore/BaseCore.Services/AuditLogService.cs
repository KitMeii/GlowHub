using BaseCore.Entities;
using BaseCore.Repository;
using System.Text.Json;

namespace BaseCore.Services
{
    public class AuditLogService
    {
        private readonly MySqlDbContext _db;

        public AuditLogService(MySqlDbContext db) => _db = db;

        public async Task Log(
            string? userId,
            string? userName,
            string action,
            string? entityType = null,
            string? entityId   = null,
            object? oldValue   = null,
            object? newValue   = null,
            string? ipAddress  = null)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserId     = userId,
                UserName   = userName,
                Action     = action,
                EntityType = entityType,
                EntityId   = entityId,
                OldValue   = oldValue != null ? JsonSerializer.Serialize(oldValue) : null,
                NewValue   = newValue != null ? JsonSerializer.Serialize(newValue) : null,
                IpAddress  = ipAddress,
                CreatedAt  = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
    }
}
