using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Questions.Infrastructure.Persistence;

namespace Questions.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddQuestionsInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<QuestionsDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IQuestionRepository, EfQuestionRepository>();
        return services;
    }
}
