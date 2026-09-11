using Bookworm.Models;
using Bookworm.Services;
using Bookworm.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;

namespace Bookworm.Controllers
{
    public class HomeController : Controller
    {
        private readonly IBookService _bookService;
        private readonly IRecommendationService _recommendationService;

        public HomeController(IBookService bookService, IRecommendationService recommendationService)
        {
            _bookService = bookService;
            _recommendationService = recommendationService;
        }

        [Authorize]
        public IActionResult Index()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            float userId = string.IsNullOrEmpty(userIdStr) ? 1 : float.Parse(userIdStr);

            var recommended = _recommendationService.GetRecommendationsForUser(userId, 20).ToList();
            var trending    = _bookService.GetTrendingBooks(20).ToList();
            var topRated    = _bookService.GetTopRatedBooks(20).ToList();
            var newReleases = _bookService.GetNewReleases(20).ToList();
            var genres      = _bookService.GetAllGenres().ToList();

            // Build genre spotlights — pick 5 featured genres
            var featuredGenres = new[] { "Fantasy", "Science Fiction", "Mystery", "Romance", "Thriller" };
            var genreSpotlights = featuredGenres.ToDictionary(
                g => g,
                g => _bookService.GetBooksByGenre(g, 1, 8, "trending")
            );

            var hero = recommended.FirstOrDefault() ?? trending.FirstOrDefault();

            var viewModel = new HomeViewModel
            {
                RecommendedBooks = recommended,
                TrendingBooks    = trending,
                TopRatedBooks    = topRated,
                NewReleases      = newReleases,
                GenreSpotlights  = genreSpotlights,
                AllGenres        = genres,
                HeroBook         = hero
            };

            return View(viewModel);
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
