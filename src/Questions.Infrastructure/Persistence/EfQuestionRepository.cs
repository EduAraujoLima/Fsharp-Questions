using Microsoft.EntityFrameworkCore;
using Questions.Core.Contracts;

namespace Questions.Infrastructure.Persistence;

public sealed class EfQuestionRepository(QuestionsDbContext db) : IQuestionRepository
{
    public async Task<IReadOnlyList<QuestionSnapshot>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await db.Questions.AsNoTracking().ToListAsync(cancellationToken);
        return entities.Select(e => e.ToSnapshot()).ToList();
    }

    public async Task<IReadOnlyList<QuestionSnapshot>> ListByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var wanted = ids.Distinct().ToList();
        var entities = await db.Questions.AsNoTracking()
            .Where(q => wanted.Contains(q.Id))
            .ToListAsync(cancellationToken);
        return entities.Select(e => e.ToSnapshot()).ToList();
    }

    public async Task<QuestionSnapshot?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Questions.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        return entity?.ToSnapshot();
    }

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        db.Questions.AnyAsync(cancellationToken);

    public async Task AddAsync(QuestionSnapshot question, CancellationToken cancellationToken = default)
    {
        db.Questions.Add(question.ToEntity());
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SaveOutcome> UpdateAsync(QuestionSnapshot question, CancellationToken cancellationToken = default)
    {
        var entity = await db.Questions.FirstOrDefaultAsync(q => q.Id == question.Id, cancellationToken);
        if (entity is null)
        {
            return SaveOutcome.NotFound;
        }

        // O núcleo F# gera a versão N+1 a partir da versão N; se a versão gravada não for N,
        // alguém alterou a questão no meio do caminho.
        if (entity.Version != question.Version - 1)
        {
            return SaveOutcome.Conflict;
        }

        question.CopyTo(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return SaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            return SaveOutcome.Conflict;
        }
    }

    public async Task<SaveOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await db.Questions.Where(q => q.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted > 0 ? SaveOutcome.Saved : SaveOutcome.NotFound;
    }
}
