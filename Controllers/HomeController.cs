using Microsoft.AspNetCore.Mvc;
using ChatApp.Services;

namespace ChatApp.Controllers;

public class HomeController : Controller
{
    private readonly IUserService _userService;

    public HomeController(IUserService userService)
    {
        _userService = userService;
    }

    public IActionResult Index()
    {
        // Debug için authentication durumunu logla
        Console.WriteLine($"🏠 Home/Index: User.Identity.IsAuthenticated = {User.Identity?.IsAuthenticated}");
        Console.WriteLine($"🏠 Home/Index: User.Identity.Name = {User.Identity?.Name}");
        
        // Kullanıcı giriş yapmışsa Chat'e yönlendir
        if (User.Identity?.IsAuthenticated == true)
        {
            Console.WriteLine($"🏠 Home/Index: Redirecting authenticated user to Chat");
            return RedirectToAction("Index", "Chat");
        }
        
        Console.WriteLine($"🏠 Home/Index: Showing login form for unauthenticated user");
        // Giriş yapmamışsa ana sayfada login formunu göster
        ViewBag.Title = "ChatApp - Hoş Geldiniz";
        return View();
    }
}
