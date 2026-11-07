using Microsoft.EntityFrameworkCore;

namespace Questions.Infrastructure.Persistence;

public sealed class QuestionsDbContext(DbContextOptions<QuestionsDbContext> options) : DbContext(options)
{
    public DbSet<QuestionEntity> Questions => Set<QuestionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuestionEntity>(question =>
        {
            question.ToTable("Questions");
            question.HasKey(q => q.Id);
            question.Property(q => q.Type).HasMaxLength(32).IsRequired();
            question.Property(q => q.Statement).HasMaxLength(4000).IsRequired();
            question.Property(q => q.Difficulty).HasMaxLength(16).IsRequired();
            // Controle de concorrência otimista: duas alterações simultâneas não se sobrescrevem.
            question.Property(q => q.Version).IsConcurrencyToken();
            question.HasIndex(q => q.Type);
            question.OwnsMany(q => q.Options, option => option.ToJson());
        });
    }
}
