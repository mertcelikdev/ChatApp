using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ChatApp.Services;
using System.Security.Claims;

namespace ChatApp.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class BlockingController : ControllerBase
    {
        private readonly IBlockingService _blockingService;
        private readonly IUserService _userService;

        public BlockingController(IBlockingService blockingService, IUserService userService)
        {
            _blockingService = blockingService;
            _userService = userService;
        }

        [HttpPost("block")]
        public async Task<IActionResult> BlockUser([FromBody] BlockUserRequest request)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            if (userId == 0)
                return Unauthorized(new { success = false, message = "Oturum açın" });

            // Target user'ı bul
            var targetUser = await _userService.GetUserByUsernameAsync(request.Username);
            if (targetUser == null)
                return BadRequest(new { success = false, message = "Kullanıcı bulunamadı" });

            var result = await _blockingService.BlockUserAsync(userId, targetUser.Id, request.Reason);
            
            if (result)
                return Ok(new { success = true, message = $"{request.Username} engellendi" });
            else
                return BadRequest(new { success = false, message = "Kullanıcı engellenemedi" });
        }

        [HttpPost("unblock")]
        public async Task<IActionResult> UnblockUser([FromBody] UnblockUserRequest request)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            if (userId == 0)
                return Unauthorized(new { success = false, message = "Oturum açın" });

            // Target user'ı bul
            var targetUser = await _userService.GetUserByUsernameAsync(request.Username);
            if (targetUser == null)
                return BadRequest(new { success = false, message = "Kullanıcı bulunamadı" });

            var result = await _blockingService.UnblockUserAsync(userId, targetUser.Id);
            
            if (result)
                return Ok(new { success = true, message = $"{request.Username} engeli kaldırıldı" });
            else
                return BadRequest(new { success = false, message = "Engel kaldırılamadı" });
        }

        [HttpGet("blocked")]
        public async Task<IActionResult> GetBlockedUsers()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            if (userId == 0)
                return Unauthorized(new { success = false, message = "Oturum açın" });

            var blockedUsers = await _blockingService.GetBlockedUsersAsync(userId);
            return Ok(new { success = true, blockedUsers });
        }

        [HttpPost("report")]
        public async Task<IActionResult> ReportUser([FromBody] ReportUserRequest request)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            if (userId == 0)
                return Unauthorized(new { success = false, message = "Oturum açın" });

            // Target user'ı bul
            var targetUser = await _userService.GetUserByUsernameAsync(request.Username);
            if (targetUser == null)
                return BadRequest(new { success = false, message = "Kullanıcı bulunamadı" });

            var result = await _blockingService.ReportUserAsync(userId, targetUser.Id, request.Reason, request.Category);
            
            if (result)
                return Ok(new { success = true, message = $"{request.Username} şikayet edildi" });
            else
                return BadRequest(new { success = false, message = "Şikayet gönderilemedi" });
        }

        [HttpGet("reports")]
        public async Task<IActionResult> GetUserReports()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            if (userId == 0)
                return Unauthorized(new { success = false, message = "Oturum açın" });

            var reports = await _blockingService.GetUserReportsAsync(userId);
            return Ok(new { success = true, reports });
        }

        [HttpGet("admin/reports")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetAllReports()
        {
            var reports = await _blockingService.GetAllReportsAsync();
            return Ok(new { success = true, reports });
        }

        [HttpPost("admin/resolve/{reportId}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> ResolveReport(int reportId, [FromBody] ResolveReportRequest request)
        {
            var result = await _blockingService.ResolveReportAsync(reportId, request.AdminNotes);
            
            if (result)
                return Ok(new { success = true, message = "Şikayet çözüldü" });
            else
                return BadRequest(new { success = false, message = "Şikayet çözülemedi" });
        }
    }

    // DTOs
    public class BlockUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    public class UnblockUserRequest
    {
        public string Username { get; set; } = string.Empty;
    }

    public class ReportUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
    }

    public class ResolveReportRequest
    {
        public string? AdminNotes { get; set; }
    }
}
