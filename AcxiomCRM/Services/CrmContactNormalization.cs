using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public static class CrmContactNormalization
{
    public static string NormalizeEmail(string value) => value.Trim().ToUpperInvariant();

    public static string NormalizePhone(string value) => new(value.Where(character => character is >= '0' and <= '9').ToArray());

    public static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteErrorCode: 19 };
}
