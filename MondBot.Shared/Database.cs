using Npgsql;

namespace MondBot
{
    public static class Database
    {
        private static readonly NpgsqlConnectionStringBuilder ConnectionStr;

        static Database()
        {
            ConnectionStr = new NpgsqlConnectionStringBuilder
            {
                Host = Settings.Instance.DbAddress,
                Port = Settings.Instance.DbPort,
                Database = Settings.Instance.DbName,
                Username = Settings.Instance.DbUsername,
                Password = Settings.Instance.DbPassword,

                Pooling = true,
                MinPoolSize = 1,
                MaxPoolSize = 20
            };
        }

        public static NpgsqlConnection CreateConnection()
        {
            var connection = new NpgsqlConnection(ConnectionStr);
            connection.Open();
            return connection;
        }
    }
}
