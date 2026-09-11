using System.Collections.Generic;
using Bookworm.Models;

namespace Bookworm.ViewModels
{
    public class BookDetailsViewModel
    {
        public Book? Book { get; set; }

        public IEnumerable<Book> RecommendedSimilarBooks { get; set; }
            = new List<Book>();

        public bool IsLiked { get; set; }
        public ReadingProgress? ReadingProgress { get; set; }

        public IEnumerable<BookReviewViewModel> Reviews { get; set; }
            = new List<BookReviewViewModel>();
    }
}