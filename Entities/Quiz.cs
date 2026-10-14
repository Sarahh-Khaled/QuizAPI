namespace QuizAPI.Entities
{
    public class Quiz
    {

        public int Id { get; set; }

        public string? Title { get; set; }
        public string? Description { get; set; }
        public int DurationMinutes { get; set; }
        public DateTime Deadline { get; set; }
        public bool IsPublished { get; set; }

        public string TeacherId { get; set; } = string.Empty;
        
        // Navigation Properties:
        public ApplicationUser Teacher { get; set; } = null!;
        public List<Question> Questions { get; set; } = new();
        public List<Attempt> Attempts { get; set; } = new();





    }
}
