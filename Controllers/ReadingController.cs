using Bookworm.Data;
using Bookworm.Services;
using Bookworm.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;

namespace Bookworm.Controllers
{
    [Authorize]
    public class ReadingController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IBookService _bookService;

        public ReadingController(AppDbContext db, IBookService bookService)
        {
            _db = db;
            _bookService = bookService;
        }

        public IActionResult Dashboard()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdStr);

            var readingProgress = _db.ReadingProgress
                .Where(r => r.UserId == userId)
                .ToList();

            var currentlyReadingIds = readingProgress
                .Where(r => r.Status == "Currently Reading")
                .Select(r => r.BookId)
                .ToList();

            var completedIds = readingProgress
                .Where(r => r.Status == "Completed")
                .Select(r => r.BookId)
                .ToList();

            var currentlyReadingBooks = _bookService
                .GetBooksByIds(currentlyReadingIds);

            var completedBooks = _bookService
                .GetBooksByIds(completedIds);

            var user = _db.Users.FirstOrDefault(u => u.Id == userId);

            var likedBookIds = user?.LikedBookIds?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToList()
                ?? new List<int>();

            var likedBooks = _bookService
                .GetBooksByIds(likedBookIds)
                .ToList();

            var averageProgress = readingProgress.Any()? readingProgress.Average(r => r.Progress): 0;

            var myReviews = _db.BookReviews
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new BookReviewViewModel
                {
                    UserId = r.UserId,
                    ReviewText = r.ReviewText,
                    CreatedAt = r.CreatedAt,
                    BookId = r.BookId
                })
                .ToList();

            var viewModel = new ReadingDashboardViewModel
            {
                CompletedBooks = completedBooks.Count(),
                CurrentlyReading = currentlyReadingBooks.Count(),
                ReviewsWritten = myReviews.Count,
                LikedBooksCount = likedBooks.Count(),
                AverageProgress = averageProgress,

                CurrentlyReadingBooks = currentlyReadingBooks,
                CompletedBooksList = completedBooks,
                LikedBooks = likedBooks,
                MyReviews = myReviews
            };

            return View(viewModel);
        }
    }
}