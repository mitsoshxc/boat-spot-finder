using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using BoatSpotFinder.Web.Models;

namespace BoatSpotFinder.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.IsInRole("Admin"))
        {
            return RedirectToAction("Dashboard", "Admin");
        }
        if (User.IsInRole("PlaceOwner"))
        {
            return RedirectToAction("Index", "Marinas");
        }
        if (User.IsInRole("BoatOwner"))
        {
            return Redirect("/browse");
        }
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
