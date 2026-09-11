using Bookworm.Models;
using Microsoft.EntityFrameworkCore;

namespace Bookworm.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<AppUser> Users { get; set; }
        public DbSet<BookReview> BookReviews { get; set; }
        public DbSet<ReadingProgress> ReadingProgress { get; set; }
    }
}
