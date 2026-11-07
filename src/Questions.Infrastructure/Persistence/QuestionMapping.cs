using Questions.Core.Contracts;
using Questions.Infrastructure.Interop;

namespace Questions.Infrastructure.Persistence;

/// <summary>Tradução entre o modelo de persistência e o snapshot imutável do núcleo F#.</summary>
internal static class QuestionMapping
{
    public static QuestionSnapshot ToSnapshot(this QuestionEntity entity) =>
        new(
            entity.Id,
            entity.Type,
            entity.Statement,
            entity.Difficulty,
            entity.Options
                .OrderBy(o => o.Number)
                .Select(o => new OptionSnapshot(o.Number, o.Text, o.IsCorrect))
                .ToFSharpList(),
            entity.Version,
            entity.CreatedAt,
            entity.UpdatedAt);

    public static QuestionEntity ToEntity(this QuestionSnapshot snapshot)
    {
        var entity = new QuestionEntity { Id = snapshot.Id };
        snapshot.CopyTo(entity);
        return entity;
    }

    public static void CopyTo(this QuestionSnapshot snapshot, QuestionEntity entity)
    {
        entity.Type = snapshot.Type;
        entity.Statement = snapshot.Statement;
        entity.Difficulty = snapshot.Difficulty;
        entity.Options = snapshot.Options
            .Select(o => new OptionEntity { Number = o.Id, Text = o.Text, IsCorrect = o.IsCorrect })
            .ToList();
        entity.Version = snapshot.Version;
        entity.CreatedAt = snapshot.CreatedAt;
        entity.UpdatedAt = snapshot.UpdatedAt;
    }
}
