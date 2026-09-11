using Bookworm.Data;
using Bookworm.Models;
using Bookworm.Services;
using Bookworm.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Bookworm.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IBookService _bookService;
        private readonly IRecommendationService _recommendationService;

        private static readonly string[] AllGenres = new[]
        {
            "Fantasy", "Science Fiction", "Mystery", "Thriller", "Romance",
            "Horror", "Historical", "Non-Fiction", "Young Adult", "Classic",
            "Adventure", "Biography", "Self-Help", "Children's", "General Fiction"
        };

        public AccountController(AppDbContext db, IBookService bookService, IRecommendationService recommendationService)
        {
            _db = db;
            _bookService = bookService;
            _recommendationService = recommendationService;
        }


        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Username and password are required.";
                return View();
            }

            if (_db.Users.Any(u => u.Username == username))
            {
                ViewBag.Error = "Username already exists.";
                return View();
            }

            var user = new AppUser
            {
                Username = username,
                PasswordHash = password // In production, hash this!
            };

            _db.Users.Add(user);
            _db.SaveChanges();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Onboarding");
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Username and password are required.";
                return View();
            }

            var user = _db.Users.FirstOrDefault(u => u.Username == username && u.PasswordHash == password);
            if (user == null)
            {
                ViewBag.Error = "Invalid username or password.";
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Onboarding()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Onboarding(string[] genres)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Register");

            var user = _db.Users.Find(int.Parse(userIdStr));
            if (user != null)
            {
                user.PreferredGenres = string.Join(",", genres);
                _db.SaveChanges();
            }

            return RedirectToAction("SelectBooks");
        }

        [HttpGet]
        public IActionResult SelectBooks()
        {
            // Pass all books sorted by trending — JS will filter them client-side
            var books = _bookService.GetTrendingBooks(200);
            return View(books);
        }

        [HttpPost]
        public IActionResult SelectBooks(int[] selectedBooks)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Register");

            var user = _db.Users.Find(int.Parse(userIdStr));
            if (user != null)
            {
                user.LikedBookIds = string.Join(",", selectedBooks);
                _db.SaveChanges();
            }

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [Authorize]
        [HttpGet]
        public IActionResult Profile()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login");

            var user = _db.Users.Find(int.Parse(userIdStr));
            if (user == null) return RedirectToAction("Login");

            var selectedGenres = (user.PreferredGenres ?? "")
                .Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var likedBookIds = (user.LikedBookIds ?? "")
                .Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                .Where(s => int.TryParse(s, out _))
                .Select(int.Parse)
                .ToList();

            var likedBooks = _bookService.GetBooksByIds(likedBookIds).ToList();
            // Pass top 200 popular books for the modal picker
            var popularBooks = _bookService.GetTrendingBooks(200).ToList();

            var viewModel = new ProfileViewModel
            {
                User           = user,
                AllGenres      = AllGenres.ToList(),
                SelectedGenres = selectedGenres,
                LikedBooks     = likedBooks,
                PopularBooks   = popularBooks,
                SuccessMessage = TempData["SuccessMessage"] as string
            };

            return View(viewModel);
        }

        [Authorize]
        [HttpPost]
        public IActionResult SaveProfile(string[] genres, int[] likedBookIds)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login");

            var user = _db.Users.Find(int.Parse(userIdStr));
            if (user != null)
            {
                user.PreferredGenres = string.Join(",", genres ?? Array.Empty<string>());
                user.LikedBookIds    = string.Join(",", likedBookIds ?? Array.Empty<int>());
                _db.SaveChanges();

                // Clear cached recommendations so they recompute with new preferences
                _recommendationService.ClearCacheForUser(float.Parse(userIdStr));
            }

            TempData["SuccessMessage"] = "✅ Your preferences have been updated! Refreshing recommendations…";
            return RedirectToAction("Profile");
        }

    }
}
