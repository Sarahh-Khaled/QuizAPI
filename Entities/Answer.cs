namespace QuizAPI.Entities
{
    public class Answer
    {
        public int Id { get; set; }
        public int AttemptId { get; set; }
        public Attempt Attempt { get; set; } = null!;
        public int QuestionId { get; set; }
        public Question Question { get; set; } = null!;
        public int SelectedOptionId { get; set; }
        public Option SelectedOption { get; set; } = null!;

    }
}
