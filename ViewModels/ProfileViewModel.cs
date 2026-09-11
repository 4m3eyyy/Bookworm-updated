using System.Collections.Generic;
using Bookworm.Models;

namespace Bookworm.ViewModels
{
    public class ProfileViewModel
    {
        public AppUser User { get; set; } = new();

        // All available genres for the toggle grid
        public List<string> AllGenres { get; set; } = new();

        // Currently selected genres (parsed from User.PreferredGenres)
        public HashSet<string> SelectedGenres { get; set; } = new();

        // Books the user liked during onboarding
        public List<Book> LikedBooks { get; set; } = new();

        // Available popular books for re-selection
        public List<Book> PopularBooks { get; set; } = new();

        // Feedback message after save
        public string? SuccessMessage { get; set; }
    }
}
