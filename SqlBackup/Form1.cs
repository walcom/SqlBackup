using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Xml.Linq;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Common;
using System.IO;
using System.Threading.Tasks;
//using SQLDMO;

namespace SqlBackup
{
    public partial class Form1 : Form
    {
        private string dateTimePart;

        public Form1()
        {
            InitializeComponent();

            dateTimePart = string.Format("{0}{1}{2}_{3}{4}", GetNumberCode(DateTime.Now.Year), GetNumberCode(DateTime.Now.Month),
                GetNumberCode(DateTime.Now.Day), GetNumberCode(DateTime.Now.Hour), GetNumberCode(DateTime.Now.Minute));
        }


        private string GetNumberCode(int number)
        {
            return (number < 10) ? string.Format("0{0}", number) : number.ToString();
        }


        /// <summary>
        /// To backup Database
        /// </summary>
        private void DoDatabaseBackup()
        {
            string message = "";

            try
            {
                //SQLDMO.SQLServer server = new SQLServer();
                //var directory = Directory.GetCurrentDirectory();
                var currentDir = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory.ToString());
                //WriteInEventLog("Path: " + currentDir, EventLogEntryType.Information);

                // Environment.CurrentDirectory 
                string filePath = currentDir + "\\Databases.xml"; // HostingEnvironment.ApplicationPhysicalPath 

                EventLog elog;
                Server sqlServer;
                Database db;
                ServerConnection connection;
                BackupDeviceItem deviceItem;
                Backup backup = new Backup();
                IEnumerable<BackupDatabase> query;

                try
                {
                     query = from e in XElement.Load(filePath).Elements("BackupDatabase")
                                select new BackupDatabase
                                {
                                    BackupPath = (string)e.Element("BackupPath"),
                                    ServerName = (string)e.Element("ServerName"),
                                    DBName = (string)e.Element("DBName"),
                                    UserName = (string)e.Element("UserName"),
                                    Password = (string)e.Element("Password"),
                                    Compressed = (int)e.Element("Compressed")
                                };
                }
                catch (Exception)
                {
                    query = from e in XElement.Load(filePath).Elements("BackupDatabase")
                            select new BackupDatabase
                            {
                                BackupPath = (string)e.Element("BackupPath"),
                                ServerName = (string)e.Element("ServerName"),
                                DBName = (string)e.Element("DBName"),
                                UserName = (string)e.Element("UserName"),
                                Password = (string)e.Element("Password"),
                                Compressed = 0
                            };
                }

               
               

                string backupPath = "", backupFileName = "", compressedFileName = "";
                string backupDestination = "", compressedDestination = "";
                List<BackupDatabase> list = query.ToList();
                foreach (BackupDatabase dbToBackup in list)
                {
                    //BackupDatabase dbToBackup = list[0];

                    try
                    {
                        backupPath = dbToBackup.BackupPath;
                        backupFileName = string.Format("{0}_DB_{1}.bak", dbToBackup.DBName, dateTimePart);
                        compressedFileName = backupFileName + ".gz";
                        backupDestination = string.Format("{0}{1}", backupPath, backupFileName);
                        compressedDestination = string.Format("{0}{1}", backupPath, compressedFileName);

                        backup.Action = BackupActionType.Database;
                        backup.BackupSetDescription = string.Format("Backup of {0} on {1}", dbToBackup.DBName, dateTimePart);
                        backup.BackupSetName = "FullBackup";
                        backup.Database = dbToBackup.DBName;

                        deviceItem = new BackupDeviceItem(backupDestination, DeviceType.File);

                        // define server connection
                        connection = new ServerConnection(@dbToBackup.ServerName, dbToBackup.UserName, dbToBackup.Password);
                        connection.LoginSecure = false;

                        sqlServer = new Server(connection);
                        sqlServer.ConnectionContext.StatementTimeout = 60 * 60;
                        sqlServer.ConnectionContext.Connect();

                        using (elog = new EventLog("Application"))
                        {
                            message = "Connected successfully to " + dbToBackup.ServerName;
                            elog.Source = "Application";
                            elog.WriteEntry(message, EventLogEntryType.Information, 101, 1);
                        }

                        db = sqlServer.Databases[dbToBackup.DBName];

                        backup.Initialize = true;
                        backup.Checksum = true;
                        backup.ContinueAfterError = true;
                        backup.Devices.Add(deviceItem);

                        backup.Incremental = false;
                        backup.ExpirationDate = DateTime.Today.AddDays(180);
                        backup.LogTruncation = BackupTruncateLogType.Truncate;
                        backup.FormatMedia = false;

                        backup.SqlBackup(sqlServer);
                        backup.Devices.Remove(deviceItem);

                        if (dbToBackup.Compressed == 1)
                        {
                            // Start compression in a separate thread
                            Task.Run(() => CompressBackupFile(backupDestination, compressedDestination))
                                .ContinueWith(task =>
                                {
                                    if (task.Exception != null)
                                    {
                                        WriteInEventLog($"Compression failed: {task.Exception.InnerException?.Message}",
                                            EventLogEntryType.Warning);
                                    }
                                    else
                                    {
                                        message = $"Backup has been compressed into the file: {compressedDestination}";
                                        WriteInEventLog(message, EventLogEntryType.Information);
                                    }
                                }, TaskScheduler.FromCurrentSynchronizationContext());
                        }


                        string messageTitle = string.Format("{0} Backup Tool", dbToBackup.DBName);
                        message = string.Format("Backup has been taken successfully into the file: {0}{1}", backupPath, backupFileName);
                        WriteInEventLog(message, EventLogEntryType.Information);
                    }
                    catch (Exception ex)
                    {
                        message = string.Format("Error in Database Backup Tool For Database: {0} -- {1} -- {2} -- {3}",
                            dbToBackup.DBName, ex.Message, ex.StackTrace, ex.InnerException); // 

                        WriteInEventLog(message, EventLogEntryType.Warning);
                        continue;

                        //using (EventLog elog = new EventLog("Application"))
                        //{
                        //    elog.Source = "Application";
                        //    elog.WriteEntry(message, EventLogEntryType.Warning, 101, 1);
                        //}

                        //EventLog.WriteEntry("Error in Database Backup Tool For Database: " + dbToBackup.DBName, ex.Message, EventLogEntryType.Warning);                        
                    }
                }
            }
            catch (Exception ex)
            {
                message = string.Format("Error in Database Backup Tool  {0}", ex.Message);

                WriteInEventLog(message, EventLogEntryType.Warning);
                //using (EventLog elog = new EventLog("Application"))
                //{                   
                //    elog.Source = "Application";
                //    elog.WriteEntry(message, EventLogEntryType.Warning, 101, 1);
                //}

                //EventLog.WriteEntry("Error in Database Backup Tool", ex.Message, EventLogEntryType.Warning);
            }
        }

        private void CompressBackupFile(string sourceFile, string destinationFile)
        {
            try
            {
                using (FileStream originalFileStream = File.OpenRead(sourceFile))
                using (FileStream compressedFileStream = File.Create(destinationFile))
                using (System.IO.Compression.GZipStream compressionStream =
                    new System.IO.Compression.GZipStream(compressedFileStream, System.IO.Compression.CompressionLevel.Fastest)) //Optimal
                {
                    originalFileStream.CopyTo(compressionStream);
                }

                // Delete the original file after successful compression
                if (File.Exists(destinationFile))
                    File.Delete(sourceFile);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error compressing file: {ex.Message}", ex);
            }
        }


        private void WriteInEventLog(string message, EventLogEntryType type)
        {
            using (EventLog elog = new EventLog("Application"))
            {
                elog.Source = "Application";
                elog.WriteEntry(message, type, 101, 1);
            }
        }



        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                DoDatabaseBackup();
            }
            catch (Exception)
            {

            }
            finally
            {
                //Application.Exit();
                System.Windows.Forms.Application.Exit();
            }
        }


    }
}
