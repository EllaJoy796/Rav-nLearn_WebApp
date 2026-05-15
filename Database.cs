using Npgsql;
using Microsoft.Extensions.Configuration;

namespace RavnLearnWeb
{
    public class Database
    {
        private static string connString =
            new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true) // optional na lang
                .AddEnvironmentVariables() // ✅ Kunin sa environment variables
                .Build()
                .GetConnectionString("DefaultConnection")!;

        public static NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(connString);
        }
    }
}