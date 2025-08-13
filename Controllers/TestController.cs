using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ChatApp.Services;

namespace ChatApp.Controllers
{
    [Authorize]
    public class TestController : Controller
    {
        private readonly IS3Service _s3Service;
        private readonly IUserService _userService;

        public TestController(IS3Service s3Service, IUserService userService)
        {
            _s3Service = s3Service;
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> S3Test()
        {
            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    return Json(new { success = false, message = "Kullanıcı bulunamadı" });
                }

                var user = await _userService.GetUserByUsernameAsync(username);
                if (user == null)
                {
                    return Json(new { success = false, message = "Kullanıcı bulunamadı" });
                }

                var defaultUrl = _s3Service.GetDefaultAvatarUrl();
                
                return Json(new { 
                    success = true, 
                    message = "S3 Service çalışıyor",
                    user = new {
                        id = user.Id,
                        username = user.Username,
                        profileImageUrl = user.ProfileImageUrl ?? defaultUrl,
                        defaultAvatarUrl = defaultUrl
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"S3 Test Error: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadTest(IFormFile file)
        {
            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    return Json(new { success = false, message = "Kullanıcı bulunamadı" });
                }

                var user = await _userService.GetUserByUsernameAsync(username);
                if (user == null)
                {
                    return Json(new { success = false, message = "Kullanıcı bulunamadı" });
                }

                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, message = "Dosya seçilmedi" });
                }

                var imageUrl = await _s3Service.UploadProfileImageAsync(file, user.Id);
                
                return Json(new { 
                    success = true, 
                    message = "Dosya başarıyla yüklendi",
                    imageUrl = imageUrl
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Upload Error: {ex.Message}" });
            }
        }
    }
}
