using ChatApp.Data;
using ChatApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Services
{
    public interface IGroupService
    {
        Task<Group?> CreateGroupAsync(string name, string description, string? groupImageUrl, int createdByUserId, int[] memberIds, bool isPrivate = false);
        Task<Group?> GetGroupByIdAsync(int groupId);
        Task<List<Group>> GetUserGroupsAsync(int userId);
        Task<bool> AddMemberAsync(int groupId, int userId, int requestingUserId);
        Task<bool> RemoveMemberAsync(int groupId, int userId, int requestingUserId);
        Task<bool> UpdateGroupAsync(int groupId, string name, string description, string? groupImageUrl, int requestingUserId);
        Task<bool> IsUserMemberAsync(int groupId, int userId);
        Task<bool> IsUserAdminAsync(int groupId, int userId);
        Task<List<GroupMember>> GetGroupMembersAsync(int groupId);
        Task<List<GroupMessage>> GetGroupMessagesAsync(int groupId, int limit = 50);
        Task<GroupMessage?> AddGroupMessageAsync(int groupId, int fromUserId, string message, string messageType = "TEXT");
    }

    public class GroupService : IGroupService
    {
        private readonly ChatDbContext _context;

        public GroupService(ChatDbContext context)
        {
            _context = context;
        }

        public async Task<Group?> CreateGroupAsync(string name, string description, string? groupImageUrl, int createdByUserId, int[] memberIds, bool isPrivate = false)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(name) || name.Length < 3)
                    return null;

                // Check if user exists
                var creator = await _context.Users.FindAsync(createdByUserId);
                if (creator == null) return null;

                // Create group
                var group = new Group
                {
                    Name = name.Trim(),
                    Description = description?.Trim() ?? "",
                    GroupImageUrl = groupImageUrl,
                    CreatedByUserId = createdByUserId,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.Groups.Add(group);
                await _context.SaveChangesAsync();

                // Add creator as admin member
                var creatorMember = new GroupMember
                {
                    GroupId = group.Id,
                    UserId = createdByUserId,
                    IsAdmin = true,
                    IsActive = true,
                    JoinedAt = DateTime.UtcNow
                };
                _context.GroupMembers.Add(creatorMember);

                // Add other members
                foreach (var memberId in memberIds.Where(id => id != createdByUserId))
                {
                    var user = await _context.Users.FindAsync(memberId);
                    if (user != null)
                    {
                        var member = new GroupMember
                        {
                            GroupId = group.Id,
                            UserId = memberId,
                            IsAdmin = false,
                            IsActive = true,
                            JoinedAt = DateTime.UtcNow
                        };
                        _context.GroupMembers.Add(member);
                    }
                }

                await _context.SaveChangesAsync();
                return group;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating group: {ex.Message}");
                return null;
            }
        }

        public async Task<Group?> GetGroupByIdAsync(int groupId)
        {
            return await _context.Groups
                .Include(g => g.CreatedByUser)
                .Include(g => g.GroupMembers.Where(m => m.IsActive))
                    .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(g => g.Id == groupId && g.IsActive);
        }

        public async Task<List<Group>> GetUserGroupsAsync(int userId)
        {
            return await _context.Groups
                .Include(g => g.GroupMembers.Where(m => m.IsActive))
                .Where(g => g.IsActive && g.GroupMembers.Any(m => m.UserId == userId && m.IsActive))
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> AddMemberAsync(int groupId, int userId, int requestingUserId)
        {
            try
            {
                // Check if requesting user is admin
                if (!await IsUserAdminAsync(groupId, requestingUserId))
                    return false;

                // Check if user is already a member
                if (await IsUserMemberAsync(groupId, userId))
                    return false;

                // Check if user exists
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return false;

                var member = new GroupMember
                {
                    GroupId = groupId,
                    UserId = userId,
                    IsAdmin = false,
                    IsActive = true,
                    JoinedAt = DateTime.UtcNow
                };

                _context.GroupMembers.Add(member);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RemoveMemberAsync(int groupId, int userId, int requestingUserId)
        {
            try
            {
                // Check if requesting user is admin or removing themselves
                if (!await IsUserAdminAsync(groupId, requestingUserId) && requestingUserId != userId)
                    return false;

                // Don't allow removing the group creator
                var group = await _context.Groups.FindAsync(groupId);
                if (group?.CreatedByUserId == userId)
                    return false;

                var member = await _context.GroupMembers
                    .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId && m.IsActive);

                if (member == null) return false;

                member.IsActive = false;
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateGroupAsync(int groupId, string name, string description, string? groupImageUrl, int requestingUserId)
        {
            try
            {
                // Check if requesting user is admin
                if (!await IsUserAdminAsync(groupId, requestingUserId))
                    return false;

                var group = await _context.Groups.FindAsync(groupId);
                if (group == null || !group.IsActive) return false;

                group.Name = name.Trim();
                group.Description = description?.Trim() ?? "";
                if (groupImageUrl != null)
                    group.GroupImageUrl = groupImageUrl;

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> IsUserMemberAsync(int groupId, int userId)
        {
            return await _context.GroupMembers
                .AnyAsync(m => m.GroupId == groupId && m.UserId == userId && m.IsActive);
        }

        public async Task<bool> IsUserAdminAsync(int groupId, int userId)
        {
            return await _context.GroupMembers
                .AnyAsync(m => m.GroupId == groupId && m.UserId == userId && m.IsActive && m.IsAdmin);
        }

        public async Task<List<GroupMember>> GetGroupMembersAsync(int groupId)
        {
            return await _context.GroupMembers
                .Include(m => m.User)
                .Where(m => m.GroupId == groupId && m.IsActive)
                .OrderByDescending(m => m.IsAdmin)
                .ThenBy(m => m.JoinedAt)
                .ToListAsync();
        }

        public async Task<List<GroupMessage>> GetGroupMessagesAsync(int groupId, int limit = 50)
        {
            return await _context.GroupMessages
                .Include(m => m.FromUser)
                .Where(m => m.GroupId == groupId && !m.IsDeleted)
                .OrderByDescending(m => m.Timestamp)
                .Take(limit)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();
        }

        public async Task<GroupMessage?> AddGroupMessageAsync(int groupId, int fromUserId, string message, string messageType = "TEXT")
        {
            try
            {
                // Check if user is member
                if (!await IsUserMemberAsync(groupId, fromUserId))
                    return null;

                var groupMessage = new GroupMessage
                {
                    GroupId = groupId,
                    FromUserId = fromUserId,
                    Message = message,
                    MessageType = messageType,
                    Timestamp = DateTime.UtcNow
                };

                _context.GroupMessages.Add(groupMessage);
                await _context.SaveChangesAsync();

                // Return with user info
                return await _context.GroupMessages
                    .Include(m => m.FromUser)
                    .FirstOrDefaultAsync(m => m.Id == groupMessage.Id);
            }
            catch
            {
                return null;
            }
        }
    }
}
