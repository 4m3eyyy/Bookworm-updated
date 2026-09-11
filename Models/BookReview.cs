using System;
using System.ComponentModel.DataAnnotations;

namespace Bookworm.Models
{
    public class BookReview
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int BookId { get; set; }

        [Required]
        public string ReviewText { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}