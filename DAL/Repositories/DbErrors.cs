using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KrishiLink.DAL.Repositories
{
    public static class DbErrors
    {
        public static bool IsUniqueViolation(DbUpdateException ex)
        {
            return ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            };
        }
    }
}
