using System.Collections.Generic;
using Bookworm.Models;

namespace Bookworm.Services
{
    public interface IBookService
    {
        IEnumerable<Book> GetAllBooks();
        Book? GetBookById(int id);
        IEnumerable<Book> GetTrendingBooks(int count = 10);
        IEnumerable<Book> GetTopRatedBooks(int count = 10);
        IEnumerable<Book> GetNewReleases(int count = 10);
        IEnumerable<Book> SearchBooks(string query);
        IEnumerable<Book> GetBooksByGenre(string genre, int page = 1, int pageSize = 28, string sortBy = "trending");
        int GetBookCountByGenre(string genre);
        IEnumerable<string> GetAllGenres();
        IEnumerable<Book> GetBooksByIds(IEnumerable<int> ids);
    }
}
