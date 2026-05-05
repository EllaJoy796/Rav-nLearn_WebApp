using Npgsql;
using Microsoft.Extensions.Configuration;

namespace RavnLearnWeb
{
    public class Database
    {
        private static string connString =
            new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build()
                .GetConnectionString("DefaultConnection")!;

        public static NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(connString);
        }
    }
}