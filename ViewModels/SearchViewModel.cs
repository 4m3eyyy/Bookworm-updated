using System.Collections.Generic;
using Bookworm.Models;

namespace Bookworm.ViewModels
{
    public class SearchViewModel
    {
        public string Query { get; set; } = string.Empty;
        public IEnumerable<Book> Results { get; set; } = new List<Book>();
        public int TotalResults { get; set; }
    }
}
