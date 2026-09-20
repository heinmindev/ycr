using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace YCR.Application.Violations;

public sealed class ApplicationUsingSqlServerProvider
{
    public SqlConnection Connection { get; } = null!;

    public SqlServerDbContextOptionsBuilder SqlServerOptions { get; } = null!;

    public DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder) =>
        builder.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Violation");
}
