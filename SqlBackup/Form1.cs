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
using SqlBackup.Classes;
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

            dateTimePart = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            //dateTimePart = string.Format("{0}{1}{2}_{3}{4}", HelperClass.GetNumberCode(DateTime.Now.Year), HelperClass.GetNumberCode(DateTime.Now.Month),
            //    HelperClass.GetNumberCode(DateTime.Now.Day), HelperClass.GetNumberCode(DateTime.Now.Hour), HelperClass.GetNumberCode(DateTime.Now.Minute));
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            DoDatabaseBackup();

            try
            {
                if (activeTasks.Any())
                {
                    // Wait for all backup/compression tasks. No overall timeout: the process exiting
                    // would kill in-flight backups; each backup is bounded by StatementTimeout.
                    Task.WaitAll(activeTasks.ToArray());
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
                HelperClass.WriteInEventLog($"Error waiting for backup tasks: {ex.Message}", EventLogEntryType.Error);
                Environment.ExitCode = 1;
            }
            finally
            {
                Application.Exit();
            }
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
                                ServiceAccountName = (string)e.Element("ServiceAccountName"),

                                Compressed = (int)e.Element("Compressed"),
                                FinalCompressingPath = (string)e.Element("FinalCompressingPath")
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
                                ServiceAccountName = (string)e.Element("ServiceAccountName"),

                                Compressed = 0,
                                FinalCompressingPath = (string)e.Element("BackupPath")
                            };

                    list = query.ToList();
                }


                // Each database is backed up (and compressed) in its own parallel task
                foreach (BackupDatabase dbToBackup in list)
                {
                    activeTasks.Add(Task.Run(() => BackupAndCompress(dbToBackup)));
                }
            }
            catch (Exception ex)
            {
                message = string.Format("Error in Database Backup Tool  {0}", ex.Message);
                HelperClass.WriteInEventLog(message, EventLogEntryType.Warning);

                //using (EventLog elog = new EventLog("Application"))
                //{                   
                //    elog.Source = "Application";
                //    elog.WriteEntry(message, EventLogEntryType.Warning, 101, 1);
                //}

                //EventLog.WriteEntry("Error in Database Backup Tool", ex.Message, EventLogEntryType.Warning);
            }
        }

        /// <summary>
        /// Backs up a single database and, if configured, compresses the backup file.
        /// Runs on a worker thread, so it uses only local state.
        /// </summary>
        private void BackupAndCompress(BackupDatabase dbToBackup)
        {
            string message;

            try
            {
                string backupPath = dbToBackup.BackupPath;
                string compressingPath = dbToBackup.FinalCompressingPath;

                string backupFileName = string.Format("{0}_DB_{1}.bak", dbToBackup.DBName, dateTimePart);
                string compressedFileName = backupFileName + ".zip";
                string backupDestination = string.Format("{0}{1}", backupPath, backupFileName);
                string compressedDestination = string.Format("{0}{1}", compressingPath, compressedFileName);

                Backup backup = new Backup();
                backup.Action = BackupActionType.Database;
                backup.BackupSetDescription = string.Format("Backup of {0} on {1}", dbToBackup.DBName, dateTimePart);
                backup.BackupSetName = "FullBackup";
                backup.Database = dbToBackup.DBName;

                BackupDeviceItem deviceItem = new BackupDeviceItem(backupDestination, DeviceType.File);

                // define server connection
                ServerConnection connection = new ServerConnection(@dbToBackup.ServerName, dbToBackup.UserName, dbToBackup.Password);
                connection.LoginSecure = false;

                Server sqlServer = new Server(connection);
                sqlServer.ConnectionContext.StatementTimeout = 60 * 60;

                try
                {
                    sqlServer.ConnectionContext.Connect();

                    HelperClass.WriteInEventLog("Connected successfully to " + dbToBackup.ServerName, EventLogEntryType.Information);

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
                }
                finally
                {
                    if (sqlServer.ConnectionContext.IsOpen)
                        sqlServer.ConnectionContext.Disconnect();
                }

                message = string.Format("Backup has been taken successfully into the file: {0}{1}", backupPath, backupFileName);
                HelperClass.WriteInEventLog(message, EventLogEntryType.Information);

                if (dbToBackup.Compressed == 1)
                {
                    try
                    {
                        HelperClass.CompressBackupFile(backupDestination, compressedDestination, dbToBackup.ServiceAccountName);

                        message = $"Backup has been compressed into the file: {compressedDestination}";
                        HelperClass.WriteInEventLog(message, EventLogEntryType.Information);
                    }
                    catch (Exception ex)
                    {
                        HelperClass.WriteInEventLog($"Compression failed: {ex.InnerException?.Message ?? ex.Message}",
                            EventLogEntryType.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                message = string.Format("Error in Database Backup Tool For Database: {0} -- {1} -- {2} -- {3}",
                    dbToBackup.DBName, ex.Message, ex.StackTrace, ex.InnerException);

                HelperClass.WriteInEventLog(message, EventLogEntryType.Warning);
            }
        }




    }
}
