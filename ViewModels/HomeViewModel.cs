using System.Collections.Generic;
using Bookworm.Models;

namespace Bookworm.ViewModels
{
    public class HomeViewModel
    {
        public IEnumerable<Book> RecommendedBooks { get; set; } = new List<Book>();
        public IEnumerable<Book> TrendingBooks { get; set; } = new List<Book>();
        public IEnumerable<Book> TopRatedBooks { get; set; } = new List<Book>();
        public IEnumerable<Book> NewReleases { get; set; } = new List<Book>();
        public Dictionary<string, IEnumerable<Book>> GenreSpotlights { get; set; } = new();
        public IEnumerable<string> AllGenres { get; set; } = new List<string>();
        public Book? HeroBook { get; set; }
    }
}
