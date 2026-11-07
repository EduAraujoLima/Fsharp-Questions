using Questions.Core.Contracts;

namespace Questions.Infrastructure.Persistence;

public enum SaveOutcome
{
    Saved,
    NotFound,
    Conflict,
}

/// <summary>Porta de acesso ao banco de questões. Trabalha com os snapshots do núcleo F#.</summary>
public interface IQuestionRepository
{
    Task<IReadOnlyList<QuestionSnapshot>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuestionSnapshot>> ListByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);

    Task<QuestionSnapshot?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    Task AddAsync(QuestionSnapshot question, CancellationToken cancellationToken = default);

    /// <summary>Persiste uma nova versão. Falha com <see cref="SaveOutcome.Conflict"/> se outra
    /// alteração tiver sido gravada desde a versão anterior.</summary>
    Task<SaveOutcome> UpdateAsync(QuestionSnapshot question, CancellationToken cancellationToken = default);

    Task<SaveOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
