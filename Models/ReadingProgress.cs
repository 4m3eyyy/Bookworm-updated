namespace Bookworm.Models
{
    public class ReadingProgress
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int BookId { get; set; }

        public string Status { get; set; } = "Currently Reading";

        public int Progress { get; set; } = 0;

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }
    }
}