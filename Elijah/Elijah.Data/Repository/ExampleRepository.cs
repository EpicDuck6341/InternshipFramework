using GenericRepository.Model;
using GenericRepository.Repository;
using Microsoft.AspNetCore.Http;

namespace Elijah.Data.Repository;

public interface IExampleRepository : IRepository<ApplicationDbContext>;


public class ExampleRepository(
    ApplicationDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    HistorySettings? historySettings
)
    : Repository<ApplicationDbContext>(dbContext, httpContextAccessor, historySettings),
        IExampleRepository
{
}
