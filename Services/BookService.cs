using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Bookworm.Models;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Hosting;

namespace Bookworm.Services
{
    public class BookService : IBookService
    {
        private readonly List<Book> _books;
        private readonly List<string> _genres;

        // Genre keyword map: genre name -> keywords to look for in title/author
        private static readonly Dictionary<string, string[]> GenreKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Fantasy"]         = new[] { "magic", "wizard", "dragon", "elf", "hobbit", "ring", "throne", "kingdom", "sorcerer", "witch", "spell", "enchant", "narnia", "fantasy", "fairy", "tolkien", "rowling", "sword", "quest" },
            ["Science Fiction"] = new[] { "space", "galaxy", "robot", "android", "alien", "mars", "star", "future", "cyborg", "dystopia", "matrix", "universe", "planet", "sci-fi", "orson", "asimov", "dune", "ender", "foundation" },
            ["Mystery"]         = new[] { "murder", "detective", "mystery", "crime", "suspect", "clue", "investigate", "sherlock", "agatha", "christie", "case", "whodunit", "police", "inspector", "conan", "doyle" },
            ["Thriller"]        = new[] { "thriller", "suspense", "danger", "chase", "assassin", "conspiracy", "spy", "agent", "grisham", "clancy", "hunt", "secret", "code", "deception", "trap" },
            ["Romance"]         = new[] { "love", "heart", "passion", "romance", "kiss", "beloved", "desire", "affair", "wedding", "bride", "darcy", "austen", "nicholas sparks", "bride", "marry" },
            ["Horror"]          = new[] { "horror", "ghost", "haunted", "terror", "fear", "shadow", "dark", "evil", "demon", "vampire", "zombie", "stephen king", "nightmare", "dread", "curse" },
            ["Historical"]      = new[] { "war", "history", "ancient", "medieval", "empire", "revolution", "victorian", "century", "kingdom", "historical", "dynasty", "pharaoh", "gladiator", "tudor", "war and peace" },
            ["Non-Fiction"]     = new[] { "guide", "how to", "memoir", "biography", "autobiography", "true story", "history of", "science of", "art of", "introduction", "analysis", "world of", "malcolm gladwell", "sapiens", "yuval" },
            ["Young Adult"]     = new[] { "hunger games", "divergent", "maze runner", "twilight", "percy jackson", "teen", "young adult", "ya", "high school", "vampire diaries", "mortal instruments" },
            ["Classic"]         = new[] { "jane austen", "charles dickens", "dostoyevsky", "tolstoy", "hemingway", "fitzgerald", "shakespeare", "bronte", "orwell", "kafka", "wilde", "hardy", "hugo", "melville" },
            ["Adventure"]       = new[] { "adventure", "journey", "expedition", "treasure", "island", "pirates", "explorer", "survive", "wild", "escape", "odyssey", "robinson", "crusoe", "captain" },
            ["Biography"]       = new[] { "biography", "life of", "story of", "memoir", "autobiography", "the man", "the woman", "the making of", "einstein", "darwin", "lincoln", "gandhi", "mandela" },
            ["Self-Help"]       = new[] { "self-help", "mindset", "habits", "power of", "atomic", "thinking", "success", "leadership", "motivation", "mindfulness", "productivity", "7 habits", "dale carnegie" },
            ["Children's"]      = new[] { "children", "kids", "picture book", "bedtime", "sesame", "winnie", "pooh", "goodnight", "little prince", "roald dahl", "dr. seuss", "nursery", "fairy tale" },
        };

        public BookService(IWebHostEnvironment env)
        {
            var dataPath = Path.Combine(env.ContentRootPath, "Data", "books.csv");
            if (File.Exists(dataPath))
            {
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    MissingFieldFound = null,
                    BadDataFound = null
                };

                using var reader = new StreamReader(dataPath);
                using var csv = new CsvReader(reader, config);
                _books = csv.GetRecords<Book>().ToList();

                // Tag each book with a genre and compute Wilson Score
                foreach (var book in _books)
                {
                    book.Genre = InferGenre(book);
                    book.WilsonScore = ComputeWilsonScore(book);
                }
            }
            else
            {
                _books = new List<Book>();
            }

            _genres = _books
                .Select(b => b.Genre)
                .Distinct()
                .OrderBy(g => g)
                .ToList();
        }

        private static string InferGenre(Book book)
        {
            var searchText = $"{book.Title} {book.Authors} {book.OriginalTitle}".ToLower();

            foreach (var kvp in GenreKeywords)
            {
                if (kvp.Value.Any(keyword => searchText.Contains(keyword)))
                    return kvp.Key;
            }
            return "General Fiction";
        }

        // Wilson Score Interval — statistically better than raw rating or raw count
        private static double ComputeWilsonScore(Book book)
        {
            int n = book.RatingsCount;
            if (n == 0) return 0;

            double z = 1.96; // 95% confidence
            double phat = book.AverageRating / 5.0;
            return (phat + z * z / (2 * n) - z * Math.Sqrt((phat * (1 - phat) + z * z / (4 * n)) / n))
                   / (1 + z * z / n);
        }

        public IEnumerable<Book> GetAllBooks() => _books;

        public Book? GetBookById(int id) => _books.FirstOrDefault(b => b.BookId == id);

        public IEnumerable<Book> GetTrendingBooks(int count = 10)
        {
            return _books.OrderByDescending(b => b.WilsonScore).Take(count);
        }

        public IEnumerable<Book> GetTopRatedBooks(int count = 10)
        {
            // Only consider books with enough ratings to be meaningful (>=1000 ratings)
            return _books
                .Where(b => b.RatingsCount >= 1000)
                .OrderByDescending(b => b.AverageRating)
                .Take(count);
        }

        public IEnumerable<Book> GetNewReleases(int count = 10)
        {
            return _books
                .Where(b => b.PublicationYear >= 2010 && b.RatingsCount > 500)
                .OrderByDescending(b => b.PublicationYear)
                .ThenByDescending(b => b.WilsonScore)
                .Take(count);
        }

        public IEnumerable<Book> SearchBooks(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return Enumerable.Empty<Book>();
            var lowerQuery = query.ToLower();
            return _books
                .Where(b => b.Title.ToLower().Contains(lowerQuery) ||
                            b.Authors.ToLower().Contains(lowerQuery) ||
                            b.OriginalTitle.ToLower().Contains(lowerQuery))
                .OrderByDescending(b => b.WilsonScore);
        }

        public IEnumerable<Book> GetBooksByGenre(string genre, int page = 1, int pageSize = 28, string sortBy = "trending")
        {
            var filtered = string.IsNullOrEmpty(genre)
                ? _books.AsEnumerable()
                : _books.Where(b => b.Genre.Equals(genre, StringComparison.OrdinalIgnoreCase));

            filtered = sortBy switch
            {
                "rating"  => filtered.OrderByDescending(b => b.AverageRating),
                "newest"  => filtered.OrderByDescending(b => b.PublicationYear),
                "popular" => filtered.OrderByDescending(b => b.RatingsCount),
                _         => filtered.OrderByDescending(b => b.WilsonScore), // "trending"
            };

            return filtered.Skip((page - 1) * pageSize).Take(pageSize);
        }

        public int GetBookCountByGenre(string genre)
        {
            if (string.IsNullOrEmpty(genre)) return _books.Count;
            return _books.Count(b => b.Genre.Equals(genre, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<string> GetAllGenres() => _genres;

        public IEnumerable<Book> GetBooksByIds(IEnumerable<int> ids)
        {
            var idSet = ids.ToHashSet();
            return _books.Where(b => idSet.Contains(b.BookId));
        }
    }
}
