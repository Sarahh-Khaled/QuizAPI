namespace QuizAPI.Entities
{
    public class Question
    {

        public int Id { get; set; }
        public int QuizId { get; set; }
        public Quiz Quiz { get; set; } = null!;
        public string Text { get; set; } = string.Empty;
        public int Points { get; set; }

        public List<Option> Options { get; set; } = new();

    }
}
