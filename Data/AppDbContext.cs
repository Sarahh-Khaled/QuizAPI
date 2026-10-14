using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QuizAPI.Entities;

namespace QuizAPI.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    // Constructor
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Option> Options => Set<Option>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<Answer> Answers => Set<Answer>();


    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // one attempt per student per quiz
        builder.Entity<Attempt>()
            .HasIndex(a => new { a.QuizId, a.StudentId }) // composite primary key
            .IsUnique();

        builder.Entity<Quiz>()
            .HasOne(q => q.Teacher).WithMany() 
            .HasForeignKey(q => q.TeacherId)  // FK in the table with many side (1-M)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Attempt>()
            .HasOne(a => a.Student).WithMany()
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // نمنع multiple cascade paths في SQL Server
        builder.Entity<Answer>()
            .HasOne(a => a.Question).WithMany()
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Answer>()
            .HasOne(a => a.SelectedOption).WithMany()
            .HasForeignKey(a => a.SelectedOptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}