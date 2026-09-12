using System;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.DAL.Repositories
{
    public static class DbErrors
    {
        public static bool IsUniqueViolation(DbUpdateException ex)
        {
            if (ex.InnerException is SqlException sqlEx)
            {
                return sqlEx.Number is 2601 or 2627;
            }

            var message = ex.InnerException?.Message ?? ex.Message;
            return message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || message.Contains("2601")
                || message.Contains("2627");
        }
    }
}
