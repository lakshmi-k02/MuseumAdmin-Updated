using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MuseumAdmin.Data;
using MuseumAdmin.Models;
using Microsoft.Extensions.Logging;


namespace MuseumAdmin.Services
{
    public interface IAuditService
    {
        Task LogActionAsync(string userId, string actionType, string action, string resourceType, string? resourceId = null, string detail = "", bool success = true);
    }

    public class AuditService : IAuditService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<AuditService> _logger;

        public AuditService(IDbContextFactory<ApplicationDbContext> contextFactory, ILogger<AuditService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task LogActionAsync(string userId, string actionType, string action, string resourceType, string? resourceId = null, string detail = "", bool success = true)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                
                var log = new AuditLog
                {
                    UserId = userId,
                    ActionType = actionType,
                    Action = action,
                    ResourceType = resourceType,
                    ResourceId = resourceId,
                    Detail = detail,
                    Success = success,
                    Timestamp = DateTime.UtcNow
                };

                context.AuditLogs.Add(log);
                await context.SaveChangesAsync();

                // Also log to application logs for immediate visibility
                _logger.LogInformation("[AUDIT] {ActionType} | User: {UserId} | Action: {Action} | Resource: {ResourceType}({ResourceId}) | Detail: {Detail}", 
                    actionType, userId, action, resourceType, resourceId, detail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log audit action for user {UserId}", userId);
            }
        }
    }
}
