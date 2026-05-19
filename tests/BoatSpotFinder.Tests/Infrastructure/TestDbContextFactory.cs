using BoatSpotFinder.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BoatSpotFinder.Tests.Infrastructure;

public record TestDb(AppDbContext Context, SqliteConnection Connection) : IDisposable
{
    public void Dispose()
    {
        Context.Dispose();
        Connection.Dispose();
    }
}

public static class TestDbContextFactory
{
    public static TestDb CreateSqliteInMemory()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        return new TestDb(context, connection);
    }
}
