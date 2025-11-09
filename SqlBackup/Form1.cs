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
        private List<Task> activeTasks = new List<Task>();

        public Form1()
        {
            InitializeComponent();

            dateTimePart = string.Format("{0}{1}{2}_{3}{4}", GetNumberCode(DateTime.Now.Year), GetNumberCode(DateTime.Now.Month),
                GetNumberCode(DateTime.Now.Day), GetNumberCode(DateTime.Now.Hour), GetNumberCode(DateTime.Now.Minute));
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            DoDatabaseBackup();

            try
            {
                if (activeTasks.Any())
                {
                    // Wait for all tasks with a 10 minute timeout
                    bool completed = Task.WaitAll(activeTasks.ToArray(), TimeSpan.FromMinutes(10));
                    //if (!completed)
                    //{
                    //    WriteInEventLog("Some backup compression tasks did not complete within timeout period", EventLogEntryType.Warning);
                    //}

                    // Check for any faulted tasks
                    //var faultedTasks = activeTasks.Where(t => t.IsFaulted).ToList();
                    //if (faultedTasks.Any())
                    //{
                    //    foreach(var task in faultedTasks)
                    //    {
                    //        WriteInEventLog($"Task failed: {task.Exception?.InnerException?.Message}", EventLogEntryType.Error);
                    //    }
                    //    Environment.ExitCode = 1; // Indicate error
                    //}
                }
            }
            catch (Exception ex)
            {
                WriteInEventLog($"Error waiting for backup tasks: {ex.Message}", EventLogEntryType.Error);
                Environment.ExitCode = 1;
            }
            finally
            {
                Application.Exit();
            }
        }


        private string GetNumberCode(int number)
        {
            return (number < 10) ? string.Format("0{0}", number) : number.ToString();
        }


        /// <summary>
        /// To backup Database
        /// </summary>
        private async Task DoDatabaseBackup()
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

                List<BackupDatabase> list;
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

                    list = query.ToList();
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

                    list = query.ToList();
                }


                string backupPath = "", backupFileName = "", compressedFileName = "";
                string backupDestination = "", compressedDestination = "";

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
                            //bool isComplete = false;
                            //taskCompletionFlags.Add(false); // Add flag for new task
                            //int taskIndex = taskCompletionFlags.Count - 1;

                            Task newTask = Task.Run(() => CompressBackupFile(backupDestination, compressedDestination))
                                .ContinueWith(task =>
                                {
                                    //try 
                                    //{
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
                                    //}
                                    //finally
                                    //{
                                    //    taskCompletionFlags[taskIndex] = true; // Set completion flag
                                    //}
                                }, TaskScheduler.FromCurrentSynchronizationContext());


                            activeTasks.Add(newTask);
                            //await newTask;
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
                    new System.IO.Compression.GZipStream(compressedFileStream, System.IO.Compression.CompressionLevel.Optimal)) //Fastest 
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


    }
}
