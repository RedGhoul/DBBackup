using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBBackup
{
    public class DBAccess
    {
        private static IConfiguration configuration;

        public DBAccess()
        {
            configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .Build();
        }

        public void createLog(string msg)
        {
            using var connection = new MySqlConnection(configuration.GetConnectionString("DataConnection_dbbackuplog"));
            connection.Open();
            string statement = $"INSERT INTO dbbackuplog.logs (msg) values (\"{msg}\");";
            using var command = new MySqlCommand(statement, connection);
            command.ExecuteNonQuery();
        }


    }
}
