using Microsoft.AspNetCore.Identity;

namespace QuizAPI.Entities

{
    public class ApplicationUser :  IdentityUser
    {
        public string FullName { get; set; } = string.Empty;

    }
}
