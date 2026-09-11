using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bookworm.Data;
using Bookworm.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.ML;
using Microsoft.ML.Trainers;

namespace Bookworm.Services
{
    public class RecommendationService : IRecommendationService
    {
        private readonly MLContext _mlContext;
        private readonly IBookService _bookService;
        private readonly string _modelPath;
        private readonly string _dataPath;
        private ITransformer? _model;
        private PredictionEngine<BookRating, BookRatingPrediction>? _predictionEngine;

        // Cache: userId -> list of recommended books
        private readonly ConcurrentDictionary<float, List<Book>> _recommendationCache = new();
        private readonly AppDbContext _db;

        // ── Precomputed co-occurrence data for collaborative filtering ──
        // bookId -> set of userIds who rated it 4 or 5
        private Dictionary<int, HashSet<int>>? _bookFans;
        // Total number of distinct users in the dataset
        private int _totalUsers;

        public RecommendationService(IWebHostEnvironment env, IBookService bookService, AppDbContext db)
        {
            _mlContext = new MLContext(seed: 42);
            _bookService = bookService;
            _db = db;
            _modelPath = Path.Combine(env.ContentRootPath, "Data", "model.zip");
            _dataPath = Path.Combine(env.ContentRootPath, "Data", "ratings.csv");

            LoadModelIfExists();
            BuildCoOccurrenceIndex();
        }

        private void LoadModelIfExists()
        {
            if (File.Exists(_modelPath))
            {
                _model = _mlContext.Model.Load(_modelPath, out _);
                _predictionEngine = _mlContext.Model.CreatePredictionEngine<BookRating, BookRatingPrediction>(_model);
            }
        }

        /// <summary>
        /// Build a lookup: for each book, which users loved it (rated 4+)?
        /// This lets us do fast collaborative filtering at recommendation time.
        /// </summary>
        private void BuildCoOccurrenceIndex()
        {
            _bookFans = new Dictionary<int, HashSet<int>>();
            var allUserIds = new HashSet<int>();

            if (!File.Exists(_dataPath)) return;

            try
            {
                bool isHeader = true;
                foreach (var line in File.ReadLines(_dataPath))
                {
                    if (isHeader) { isHeader = false; continue; }
                    var parts = line.Split(',');
                    if (parts.Length < 3) continue;

                    if (int.TryParse(parts[0], out int uid) &&
                        int.TryParse(parts[1], out int bid) &&
                        int.TryParse(parts[2], out int rating))
                    {
                        allUserIds.Add(uid);
                        if (rating >= 4)
                        {
                            if (!_bookFans.ContainsKey(bid))
                                _bookFans[bid] = new HashSet<int>();
                            _bookFans[bid].Add(uid);
                        }
                    }
                }
            }
            catch { /* If parsing fails, we'll fall back to content-based only */ }

            _totalUsers = Math.Max(allUserIds.Count, 1);
        }

        public void TrainModel()
        {
            if (!File.Exists(_dataPath)) return;

            IDataView trainingDataView = _mlContext.Data.LoadFromTextFile<BookRating>(
                _dataPath,
                hasHeader: true,
                separatorChar: ',');

            var options = new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = "UserIdEncoded",
                MatrixRowIndexColumnName   = "BookIdEncoded",
                LabelColumnName            = "Label",
                NumberOfIterations         = 20,
                ApproximationRank          = 100
            };

            var pipeline = _mlContext.Transforms.Conversion
                .MapValueToKey(outputColumnName: "UserIdEncoded", inputColumnName: "UserId")
                .Append(_mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "BookIdEncoded", inputColumnName: "BookId"))
                .Append(_mlContext.Recommendation().Trainers.MatrixFactorization(options));

            _model = pipeline.Fit(trainingDataView);
            _mlContext.Model.Save(_model, trainingDataView.Schema, _modelPath);
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<BookRating, BookRatingPrediction>(_model);
        }

        /// <summary>
        /// The core recommendation algorithm — a hybrid of:
        ///   1. Content-based filtering (genre + author similarity to liked books)
        ///   2. Collaborative filtering (users who liked the same books also liked X)
        ///   3. Quality weighting (Wilson score)
        ///   4. Genre preference weighting
        /// </summary>
        private List<Book> ComputeRecommendationsForUser(float userId)
        {
            // ── Fetch user profile ──
            var user = _db.Users.FirstOrDefault(u => u.Id == (int)userId);
            if (user == null) return _bookService.GetTrendingBooks(50).ToList();

            var preferredGenres = (user.PreferredGenres ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var likedBookIds = (user.LikedBookIds ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Where(s => int.TryParse(s, out _))
                .Select(int.Parse)
                .ToHashSet();

            var likedBooks = _bookService.GetBooksByIds(likedBookIds).ToList();
            var allBooks = _bookService.GetAllBooks().ToList();

            // If user has no data at all, fall back to trending
            if (!likedBooks.Any() && !preferredGenres.Any())
            {
                return allBooks.OrderByDescending(b => b.WilsonScore).ToList();
            }

            // ── Precompute the user's taste profile ──
            // Genre distribution: how many liked books per genre
            var likedGenreCounts = likedBooks
                .GroupBy(b => b.Genre, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            // Author set from liked books
            var likedAuthors = likedBooks
                .SelectMany(b => b.Authors.Split(',').Select(a => a.Trim().ToLower()))
                .Where(a => !string.IsNullOrEmpty(a))
                .ToHashSet();

            // Average rating of liked books (to understand quality preference)
            float avgLikedRating = likedBooks.Any() ? likedBooks.Average(b => b.AverageRating) : 3.5f;

            // ── Collaborative signal: find the pool of "similar users" ──
            // These are users who also loved the user's liked books
            var similarUserVotes = new Dictionary<int, int>(); // bookId -> overlap count
            if (_bookFans != null && likedBookIds.Any())
            {
                // Find users who loved the same books
                var proximityUsers = new Dictionary<int, int>(); // datasetUserId -> how many liked books they share
                foreach (var likedId in likedBookIds)
                {
                    if (_bookFans.TryGetValue(likedId, out var fans))
                    {
                        foreach (var fan in fans)
                        {
                            proximityUsers.TryGetValue(fan, out int count);
                            proximityUsers[fan] = count + 1;
                        }
                    }
                }

                // Take top 200 most similar users (those who overlap on the most liked books)
                var topSimilarUsers = proximityUsers
                    .OrderByDescending(kv => kv.Value)
                    .Take(200)
                    .Select(kv => kv.Key)
                    .ToHashSet();

                // For every book, count how many of those similar users also loved it
                foreach (var kvp in _bookFans)
                {
                    int bookId = kvp.Key;
                    if (likedBookIds.Contains(bookId)) continue; // skip already liked
                    int overlap = kvp.Value.Count(u => topSimilarUsers.Contains(u));
                    if (overlap > 0)
                        similarUserVotes[bookId] = overlap;
                }
            }

            int maxCollabVotes = similarUserVotes.Values.Any() ? similarUserVotes.Values.Max() : 1;

            // ── Score every candidate book ──
            var scored = allBooks
                .Where(b => !likedBookIds.Contains(b.BookId)) // exclude already-liked books
                .AsParallel()
                .Select(book =>
                {
                    double score = 0;

                    // ─── Signal 1: GENRE MATCH (up to 4 points) ───
                    // Books whose genre matches the user's liked-book genre distribution
                    if (likedGenreCounts.TryGetValue(book.Genre, out int genreHits))
                    {
                        // Proportional: if 3 of 5 liked books are Fantasy, Fantasy gets 2.4 pts
                        score += 4.0 * ((double)genreHits / Math.Max(likedBooks.Count, 1));
                    }
                    // Boost if genre was explicitly selected in preferences
                    if (preferredGenres.Contains(book.Genre))
                    {
                        score += 2.0;
                    }

                    // ─── Signal 2: AUTHOR MATCH (up to 5 points) ───
                    // Same author as a liked book = very strong signal
                    var bookAuthors = book.Authors.Split(',').Select(a => a.Trim().ToLower()).ToHashSet();
                    int authorOverlap = bookAuthors.Intersect(likedAuthors).Count();
                    if (authorOverlap > 0)
                    {
                        score += 5.0; // Same author is the strongest content signal
                    }

                    // ─── Signal 3: COLLABORATIVE FILTERING (up to 4 points) ───
                    // "Users who liked the same books as you also liked THIS book"
                    if (similarUserVotes.TryGetValue(book.BookId, out int votes))
                    {
                        score += 4.0 * ((double)votes / maxCollabVotes);
                    }

                    // ─── Signal 4: QUALITY FILTER (up to 2 points) ───
                    // Wilson Score ensures we recommend well-regarded books
                    score += book.WilsonScore * 2.0;

                    // ─── Signal 5: RATING PROXIMITY (up to 1 point) ───
                    // Prefer books whose rating is close to the user's avg liked rating
                    float ratingDiff = Math.Abs(book.AverageRating - avgLikedRating);
                    score += Math.Max(0, 1.0 - (ratingDiff * 0.5));

                    // ─── Signal 6: ML MODEL (up to 1 point, secondary) ───
                    // Only used as a tiebreaker — it's unreliable for app users
                    if (_predictionEngine != null)
                    {
                        try
                        {
                            var mlScore = _predictionEngine.Predict(new BookRating
                            {
                                UserId = userId,
                                BookId = book.BookId
                            }).Score;
                            if (!float.IsNaN(mlScore))
                            {
                                // Normalize to 0-1 range (ML scores are typically 1-5)
                                score += Math.Clamp((mlScore - 1.0) / 4.0, 0, 1);
                            }
                        }
                        catch { /* ML prediction can fail for unknown users — ignore */ }
                    }

                    return (book, score);
                })
                .OrderByDescending(p => p.score)
                .Select(p => p.book)
                .ToList();

            return scored;
        }

        public IEnumerable<Book> GetRecommendationsForUser(float userId, int count = 10)
        {
            var cached = _recommendationCache.GetOrAdd(userId, id => ComputeRecommendationsForUser(id));
            return cached.Take(count);
        }

        public IEnumerable<Book> GetRecommendationsByGenre(float userId, string genre, int count = 8)
        {
            var cached = _recommendationCache.GetOrAdd(userId, id => ComputeRecommendationsForUser(id));
            return cached
                .Where(b => b.Genre.Equals(genre, StringComparison.OrdinalIgnoreCase))
                .Take(count);
        }

        public IEnumerable<Book> GetSimilarBooks(int bookId, int count = 8)
        {
            var target = _bookService.GetBookById(bookId);
            if (target == null) return Enumerable.Empty<Book>();

            var targetAuthors = target.Authors.Split(',').Select(a => a.Trim().ToLower()).ToHashSet();

            var all = _bookService.GetAllBooks().Where(b => b.BookId != bookId).ToList();

            // Collaborative signal for this specific book
            HashSet<int>? targetFans = null;
            _bookFans?.TryGetValue(bookId, out targetFans);

            var scored = all.Select(b =>
            {
                double score = 0;

                // Same genre
                if (b.Genre == target.Genre) score += 3.0;

                // Same author (strongest signal)
                var bAuthors = b.Authors.Split(',').Select(a => a.Trim().ToLower()).ToHashSet();
                if (bAuthors.Intersect(targetAuthors).Any())
                    score += 5.0;

                // Collaborative: fans of the target book also loved this one
                if (targetFans != null && _bookFans != null && _bookFans.TryGetValue(b.BookId, out var bFans))
                {
                    int overlap = targetFans.Intersect(bFans).Count();
                    if (overlap > 0)
                        score += 3.0 * Math.Min((double)overlap / 20.0, 1.0);
                }

                // Quality boost
                score += b.WilsonScore * 2.0;

                // Rating similarity
                score += Math.Max(0, 1.0 - Math.Abs(b.AverageRating - target.AverageRating) * 0.5);

                return (book: b, score);
            })
            .OrderByDescending(x => x.score)
            .Select(x => x.book)
            .Take(count);

            return scored;
        }

        public void ClearCacheForUser(float userId)
        {
            _recommendationCache.TryRemove(userId, out _);
        }
    }
}
