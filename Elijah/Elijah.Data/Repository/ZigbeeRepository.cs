using Elijah.Data.Context;
using GenericRepository.Model;
using GenericRepository.Repository;
using Microsoft.AspNetCore.Http;

namespace Elijah.Data.Repository;

public interface IZigbeeRepository : IRepository<ApplicationDbContext>;

public class ZigbeeRepository(
    ApplicationDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    HistorySettings? historySettings
)
    : Repository<ApplicationDbContext>(dbContext, httpContextAccessor, historySettings),
        IZigbeeRepository { }
