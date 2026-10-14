namespace QuizAPI.Entities
{
    public class Attempt
    {

        public int Id { get; set; }
        public int QuizId { get; set; }
        public Quiz Quiz { get; set; } = null!;

        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser Student { get; set; } = null!;

        public DateTime StartedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public int? Score { get; set; }

        public List<Answer> Answers { get; set; } = new();

    }
}
