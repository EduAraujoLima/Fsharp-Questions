namespace Questions.Infrastructure.Persistence;

/// <summary>Modelo de persistência (mundo externo). Não contém regra de negócio.</summary>
public sealed class QuestionEntity
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public List<OptionEntity> Options { get; set; } = [];
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Alternativa armazenada como JSON dentro da linha da questão.</summary>
public sealed class OptionEntity
{
    public int Number { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}
