using Meth.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Meth.Tests
{
    /// <summary>
    /// Creates a brand-new, uniquely named SQL Server database, applies every migration
    /// (Identity + tests/questions/topics) and drops it again when the tests finish.
    /// Uses LocalDB by default; set METH_TEST_SQLSERVER to a connection string
    /// (without a database name) to use another server, e.g. SQL Server in Docker.
    /// </summary>
    public sealed class SqlServerDatabaseFixture : IDisposable
    {
        private const string DefaultServer =
            @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";

        public SqlServerDatabaseFixture()
        {
            var server = Environment.GetEnvironmentVariable("METH_TEST_SQLSERVER");
            var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server) ? DefaultServer : server)
            {
                InitialCatalog = $"Meth_Tests_{Guid.NewGuid():N}",
                MultipleActiveResultSets = true
            };
            ConnectionString = builder.ConnectionString;

            using var db = CreateContext();
            db.Database.EnsureDeleted();
            db.Database.Migrate();
        }

        public string ConnectionString { get; }

        public ApplicationDbContext CreateContext() =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(ConnectionString)
                .Options);

        public void Dispose()
        {
            using var db = CreateContext();
            db.Database.EnsureDeleted();
        }
    }
}
