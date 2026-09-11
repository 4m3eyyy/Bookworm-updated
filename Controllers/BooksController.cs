using Bookworm.Services;
using Bookworm.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Security.Claims;
using Bookworm.Data;
using Bookworm.Models;

namespace Bookworm.Controllers
{
    [Authorize]
    public class BooksController : Controller
    {
        private readonly IBookService _bookService;
        private readonly IRecommendationService _recommendationService;
        private readonly AppDbContext _db;

        public BooksController(IBookService bookService, IRecommendationService recommendationService, AppDbContext db)
        {
            _bookService = bookService;
            _recommendationService = recommendationService;
            _db = db;
        }

        public IActionResult Details(int id)
        {
            var book = _bookService.GetBookById(id);

            if (book == null)
                return NotFound();

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            float userId = string.IsNullOrEmpty(userIdStr)
                ? 1
                : float.Parse(userIdStr);

            var user = _db.Users.Find((int)userId);

            var likedBookIds = user?.LikedBookIds?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                ?? Array.Empty<string>();

            bool isLiked = likedBookIds.Contains(id.ToString());
            var readingProgress = _db.ReadingProgress.FirstOrDefault(r => r.UserId == (int)userId && r.BookId == id);

            // Get all reviews for this book
            var reviews = _db.BookReviews
            .Where(r => r.BookId == id)
            .Join(
                _db.Users,
                review => review.UserId,
                user => user.Id,
                (review, user) => new BookReviewViewModel
                {
                    UserId = review.UserId,
                    Username = user.Username,
                    ReviewText = review.ReviewText,
                    CreatedAt = review.CreatedAt
                })
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

            var viewModel = new BookDetailsViewModel
            {
                Book = book,
                ReadingProgress = readingProgress,

                RecommendedSimilarBooks =
                    _recommendationService.GetSimilarBooks(id, 8),

                IsLiked = isLiked,

                // Pass reviews to the View
                Reviews = reviews
            };

            return View(viewModel);
        }
        [HttpPost]
        public IActionResult StartReading(int id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdStr);

            var readingProgress = _db.ReadingProgress
                .FirstOrDefault(r => r.UserId == userId && r.BookId == id);

            if (readingProgress == null)
            {
                readingProgress = new ReadingProgress
                {
                    UserId = userId,
                    BookId = id,
                    Status = "Currently Reading",
                    Progress = 0,
                    StartedAt = DateTime.UtcNow
                };

                _db.ReadingProgress.Add(readingProgress);
            }
            else
            {
                readingProgress.Status = "Currently Reading";
            }

            _db.SaveChanges();

            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        public IActionResult UpdateProgress(int id, int progress)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdStr);

            var readingProgress = _db.ReadingProgress
                .FirstOrDefault(r => r.UserId == userId && r.BookId == id);

            if (readingProgress != null)
            {
                readingProgress.Progress = Math.Clamp(progress, 0, 100);

                if (readingProgress.Progress == 100)
                {
                    readingProgress.Status = "Completed";
                    readingProgress.CompletedAt = DateTime.UtcNow;
                }
                else
                {
                    readingProgress.Status = "Currently Reading";
                    readingProgress.CompletedAt = null;
                }

                _db.SaveChanges();
            }

            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        public IActionResult MarkCompleted(int id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdStr);

            var readingProgress = _db.ReadingProgress
                .FirstOrDefault(r => r.UserId == userId && r.BookId == id);

            if (readingProgress != null)
            {
                readingProgress.Status = "Completed";
                readingProgress.Progress = 100;
                readingProgress.CompletedAt = DateTime.UtcNow;

                _db.SaveChanges();
            }

            return RedirectToAction("Details", new { id });
        }
        [HttpPost]
        public IActionResult ToggleLike(int id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");

            var user = _db.Users.Find(int.Parse(userIdStr));
            if (user != null)
            {
                var likedSet = (user.LikedBookIds ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .ToHashSet();

                var idStr = id.ToString();
                if (likedSet.Contains(idStr))
                {
                    likedSet.Remove(idStr);
                }
                else
                {
                    likedSet.Add(idStr);
                }

                user.LikedBookIds = string.Join(",", likedSet);
                _db.SaveChanges();

                _recommendationService.ClearCacheForUser(float.Parse(userIdStr));
            }

            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        public IActionResult AddReview(int bookId, string reviewText)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(reviewText))
            {
                return RedirectToAction("Details", new { id = bookId });
            }

            var review = new BookReview
            {
                UserId = int.Parse(userIdStr),
                BookId = bookId,
                ReviewText = reviewText.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _db.BookReviews.Add(review);
            _db.SaveChanges();

            return RedirectToAction("Details", new { id = bookId });
        }

        public IActionResult Browse(string genre = "", int page = 1, string sortBy = "trending")
        {
            const int pageSize = 28;
            var totalBooks = _bookService.GetBookCountByGenre(genre);
            var totalPages = (int)Math.Ceiling(totalBooks / (double)pageSize);

            var viewModel = new BrowseViewModel
            {
                Genre = genre,
                Books = _bookService.GetBooksByGenre(genre, page, pageSize, sortBy),
                AllGenres = _bookService.GetAllGenres(),
                CurrentPage = page,
                TotalPages = Math.Max(totalPages, 1),
                TotalBooks = totalBooks,
                SortBy = sortBy
            };

            return View(viewModel);
        }

        public IActionResult Search(string query = "")
        {
            var results = _bookService.SearchBooks(query).ToList();
            var viewModel = new SearchViewModel
            {
                Query = query,
                Results = results,
                TotalResults = results.Count
            };
            return View(viewModel);
        }
    }
}
