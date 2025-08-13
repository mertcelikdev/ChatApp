using Microsoft.EntityFrameworkCore;
using ChatApp.Data;
using ChatApp.Models;

namespace ChatApp.Services;

public interface IBlockingService
{
    Task<bool> BlockUserAsync(int userId, int blockedUserId, string? reason = null);
    Task<bool> UnblockUserAsync(int userId, int blockedUserId);
    Task<bool> IsUserBlockedAsync(int userId, int blockedUserId);
    Task<IEnumerable<object>> GetBlockedUsersAsync(int userId);
    Task<bool> ReportUserAsync(int reporterId, int reportedUserId, string reason, string category = "General");
    Task<IEnumerable<object>> GetUserReportsAsync(int userId); // Kullanıcının yaptığı şikayetler
    Task<IEnumerable<object>> GetAllReportsAsync(); // Admin için tüm şikayetler
    Task<bool> ResolveReportAsync(int reportId, string? adminNotes = null);
}

public class BlockingService : IBlockingService
{
    private readonly ChatDbContext _context;

    public BlockingService(ChatDbContext context)
    {
        _context = context;
    }

    public async Task<bool> BlockUserAsync(int userId, int blockedUserId, string? reason = null)
    {
        try
        {
            // Kendini engelleyemez
            if (userId == blockedUserId)
                return false;

            // Zaten engellenmiş mi kontrol et
            var existingBlock = await _context.BlockedUsers
                .FirstOrDefaultAsync(b => b.UserId == userId && b.BlockedUserId == blockedUserId);

            if (existingBlock != null)
                return false; // Zaten engellenmiş

            var blockedUser = new BlockedUser
            {
                UserId = userId,
                BlockedUserId = blockedUserId,
                Reason = reason,
                BlockedAt = DateTime.UtcNow
            };

            _context.BlockedUsers.Add(blockedUser);
            await _context.SaveChangesAsync();
            
            Console.WriteLine($"🚫 User {userId} blocked user {blockedUserId}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error blocking user: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UnblockUserAsync(int userId, int blockedUserId)
    {
        try
        {
            var blockedUser = await _context.BlockedUsers
                .FirstOrDefaultAsync(b => b.UserId == userId && b.BlockedUserId == blockedUserId);

            if (blockedUser == null)
                return false;

            _context.BlockedUsers.Remove(blockedUser);
            await _context.SaveChangesAsync();
            
            Console.WriteLine($"✅ User {userId} unblocked user {blockedUserId}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error unblocking user: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> IsUserBlockedAsync(int userId, int blockedUserId)
    {
        return await _context.BlockedUsers
            .AnyAsync(b => b.UserId == userId && b.BlockedUserId == blockedUserId);
    }

    public async Task<IEnumerable<object>> GetBlockedUsersAsync(int userId)
    {
        return await _context.BlockedUsers
            .Where(b => b.UserId == userId)
            .Join(_context.Users,
                block => block.BlockedUserId,
                user => user.Id,
                (block, user) => new
                {
                    blockId = block.Id,
                    userId = user.Id,
                    username = user.Username,
                    displayName = user.DisplayName,
                    profileImageUrl = user.ProfileImageUrl,
                    blockedAt = block.BlockedAt,
                    reason = block.Reason
                })
            .OrderByDescending(x => x.blockedAt)
            .ToListAsync();
    }

    public async Task<bool> ReportUserAsync(int reporterId, int reportedUserId, string reason, string category = "General")
    {
        try
        {
            // Kendini şikayet edemez
            if (reporterId == reportedUserId)
                return false;

            var report = new UserReport
            {
                ReporterId = reporterId,
                ReportedUserId = reportedUserId,
                Reason = reason,
                Category = category,
                ReportedAt = DateTime.UtcNow,
                IsResolved = false
            };

            _context.UserReports.Add(report);
            await _context.SaveChangesAsync();
            
            Console.WriteLine($"📢 User {reporterId} reported user {reportedUserId} for: {category}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error reporting user: {ex.Message}");
            return false;
        }
    }

    public async Task<IEnumerable<object>> GetUserReportsAsync(int userId)
    {
        return await _context.UserReports
            .Where(r => r.ReporterId == userId)
            .Join(_context.Users,
                report => report.ReportedUserId,
                user => user.Id,
                (report, user) => new
                {
                    reportId = report.Id,
                    reportedUserId = user.Id,
                    reportedUsername = user.Username,
                    reportedDisplayName = user.DisplayName,
                    reason = report.Reason,
                    category = report.Category,
                    reportedAt = report.ReportedAt,
                    isResolved = report.IsResolved,
                    resolvedAt = report.ResolvedAt
                })
            .OrderByDescending(x => x.reportedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<object>> GetAllReportsAsync()
    {
        return await _context.UserReports
            .Join(_context.Users,
                report => report.ReporterId,
                reporter => reporter.Id,
                (report, reporter) => new { report, reporter })
            .Join(_context.Users,
                x => x.report.ReportedUserId,
                reported => reported.Id,
                (x, reported) => new
                {
                    reportId = x.report.Id,
                    reporterUsername = x.reporter.Username,
                    reportedUsername = reported.Username,
                    reason = x.report.Reason,
                    category = x.report.Category,
                    reportedAt = x.report.ReportedAt,
                    isResolved = x.report.IsResolved,
                    adminNotes = x.report.AdminNotes,
                    resolvedAt = x.report.ResolvedAt
                })
            .OrderByDescending(x => x.reportedAt)
            .ToListAsync();
    }

    public async Task<bool> ResolveReportAsync(int reportId, string? adminNotes = null)
    {
        try
        {
            var report = await _context.UserReports.FindAsync(reportId);
            if (report == null)
                return false;

            report.IsResolved = true;
            report.AdminNotes = adminNotes;
            report.ResolvedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            
            Console.WriteLine($"✅ Report {reportId} resolved by admin");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error resolving report: {ex.Message}");
            return false;
        }
    }
}
