using System.Collections.Generic;

namespace Bookworm.Models
{
    public class AppUser
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        
        // Comma-separated list of genres they selected during onboarding
        public string PreferredGenres { get; set; } = string.Empty;
        
        // Comma-separated list of book IDs they liked during onboarding
        public string LikedBookIds { get; set; } = string.Empty;
    }
}
