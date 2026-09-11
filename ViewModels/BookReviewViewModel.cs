using System;

namespace Bookworm.ViewModels
{
    public class BookReviewViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string ReviewText { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int BookId { get; set; }
    }
}