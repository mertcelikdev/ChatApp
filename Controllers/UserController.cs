namespace ChatApp.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ChatApp.Models;
using ChatApp.Hubs;

public class UserController : Controller
{
    private readonly IHubContext<ChatHub> _hubContext;

    public UserController(IHubContext<ChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    // Login sayfası
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    // Register sayfası
    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    // Logout action
    [HttpGet]
    public IActionResult Logout()
    {
        // Çıkış yap ve login sayfasına yönlendir
        return RedirectToAction("Login");
    }
}
