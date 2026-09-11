using System.Collections.Generic;
using Bookworm.Models;

namespace Bookworm.ViewModels
{
    public class BrowseViewModel
    {
        public string Genre { get; set; } = string.Empty;
        public IEnumerable<Book> Books { get; set; } = new List<Book>();
        public IEnumerable<string> AllGenres { get; set; } = new List<string>();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalBooks { get; set; }
        public string SortBy { get; set; } = "trending";
    }
}
