using System.Collections.Generic;
using Bookworm.Models;

namespace Bookworm.Services
{
    public interface IRecommendationService
    {
        void TrainModel();
        IEnumerable<Book> GetRecommendationsForUser(float userId, int count = 10);
        IEnumerable<Book> GetSimilarBooks(int bookId, int count = 8);
        IEnumerable<Book> GetRecommendationsByGenre(float userId, string genre, int count = 8);
        void ClearCacheForUser(float userId);
    }
}
