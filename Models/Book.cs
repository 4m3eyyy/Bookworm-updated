using CsvHelper.Configuration.Attributes;

namespace Bookworm.Models
{
    public class Book
    {
        [Name("book_id")]
        public int BookId { get; set; }

        [Name("title")]
        public string Title { get; set; } = string.Empty;

        [Name("authors")]
        public string Authors { get; set; } = string.Empty;

        [Name("original_publication_year")]
        public float? PublicationYear { get; set; }

        [Name("image_url")]
        public string ImageUrl { get; set; } = string.Empty;

        [Name("average_rating")]
        public float AverageRating { get; set; }

        [Name("ratings_count")]
        public int RatingsCount { get; set; }

        [Name("original_title")]
        public string OriginalTitle { get; set; } = string.Empty;

        [Name("language_code")]
        public string LanguageCode { get; set; } = string.Empty;

        [Name("ratings_1")]
        public int Ratings1 { get; set; }
        [Name("ratings_2")]
        public int Ratings2 { get; set; }
        [Name("ratings_3")]
        public int Ratings3 { get; set; }
        [Name("ratings_4")]
        public int Ratings4 { get; set; }
        [Name("ratings_5")]
        public int Ratings5 { get; set; }

        // Computed property — inferred from title/author keywords
        [Ignore]
        public string Genre { get; set; } = "General Fiction";

        // Wilson Score — a statistically sound popularity metric
        [Ignore]
        public double WilsonScore { get; set; }
    }
}
