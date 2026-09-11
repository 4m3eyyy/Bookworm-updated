using Microsoft.ML.Data;
using CsvHelper.Configuration.Attributes;

namespace Bookworm.Models
{
    public class BookRating
    {
        [LoadColumn(0), Name("user_id")]
        public float UserId { get; set; }

        [LoadColumn(1), Name("book_id")]
        public float BookId { get; set; }

        [LoadColumn(2), Name("rating")]
        public float Label { get; set; }
    }
}
