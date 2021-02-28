using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace DBBackup
{
    class Program
    {
        private static string bucketName;
        private static IAmazonS3 s3Client;
        private static string awsAccessKeyId;
        private static string awsSecretAccessKey;

        private static IConfiguration configuration;

        static async Task Main(string[] args)
        {
            DBAccess DBLogger = new DBAccess();

            InitializeConfig();

            DBLogger.createLog("initializeConfig completed");

            s3Client = new AmazonS3Client(
                awsAccessKeyId: awsAccessKeyId,
                awsSecretAccessKey: awsSecretAccessKey,
                RegionEndpoint.USEast1
                );

            DBLogger.createLog("s3Client completed");

            List<string> listOfDBs = new List<string>();

            using (var connection = new MySqlConnection(configuration.GetConnectionString("DataConnection")))
            {
                connection.Open();

                using var command = new MySqlCommand("SHOW DATABASES;", connection);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string values = reader.GetString(0);
                    listOfDBs.Add(values);
                }
            }

            DBLogger.createLog("Found the following DBs: "
                + string.Join(",", listOfDBs));

            foreach (var item in listOfDBs)
            {
                string curFileName = item + $"__{DateTime.Now.Day.ToString() + "_" + DateTime.Now.Year.ToString() + "_" + DateTime.Now.Month.ToString()}.sql";
                string pathToSQLSecrets = Path.Combine(Directory.GetCurrentDirectory(), "sqlpwd");
                string backUpCommand = $"mysqldump --defaults-extra-file={pathToSQLSecrets}  {item} > {curFileName}";
                
                DBLogger.createLog($"using mysqldump to dump ");

                Process cmd = new Process();
                cmd.StartInfo.FileName = "cmd.exe";
                cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.StartInfo.CreateNoWindow = true;
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();
               
                cmd.StandardInput.WriteLine(backUpCommand);
                cmd.StandardInput.Flush();
                cmd.StandardInput.Close();
                
                cmd.WaitForExit();
                cmd.StandardOutput.ReadToEnd();

                
                string fileLocationToZip = Path.Combine(Directory.GetCurrentDirectory(), curFileName);
                using (ZipArchive zip = ZipFile.Open($"{curFileName}.zip", ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(fileLocationToZip, curFileName);
                }

                DBLogger.createLog($"Finished zipping {fileLocationToZip}");

                var fileTransferUtility = new TransferUtility(s3Client);
                await fileTransferUtility.UploadAsync($"{curFileName}.zip", bucketName);

                DBLogger.createLog($"Uploaded the following file {curFileName}");

                File.Delete(curFileName);
                File.Delete($"{curFileName}.zip");

                DBLogger.createLog($"Deleting the following files");
            }

            DBLogger.createLog($"Finished DB Back Process");
        }

        public static void InitializeConfig()
        {
            configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            bucketName = configuration.GetSection("AppSettings")["bucketName"];
            awsAccessKeyId = configuration.GetSection("AppSettings")["awsAccessKeyId"];
            awsSecretAccessKey = configuration.GetSection("AppSettings")["awsSecretAccessKey"];
        }
    }
}
