using Bookworm.Models;
using System.Collections.Generic;

namespace Bookworm.ViewModels
{
    public class ReadingDashboardViewModel
    {
        public int CompletedBooks { get; set; }

        public int CurrentlyReading { get; set; }

        public int ReviewsWritten { get; set; }

        public IEnumerable<Book> CurrentlyReadingBooks { get; set; }
            = new List<Book>();

        public IEnumerable<Book> CompletedBooksList { get; set; }
            = new List<Book>();

        public IEnumerable<Book> LikedBooks { get; set; }
        = new List<Book>();

        public int LikedBooksCount { get; set; }

        public double AverageProgress { get; set; }

        public IEnumerable<BookReviewViewModel> MyReviews { get; set; }
            = new List<BookReviewViewModel>();


    }
}