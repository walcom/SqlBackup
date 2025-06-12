using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Xml.Linq;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Common;
//using SQLDMO;

namespace SqlBackup
{
    public partial class Form1 : Form
    {
        private string dateTimePart;

        public Form1()
        {
            InitializeComponent();

            dateTimePart = string.Format("{0}{1}{2}_{3}{4}", GetNumberCode(DateTime.Now.Day), GetNumberCode(DateTime.Now.Month),
                GetNumberCode(DateTime.Now.Year), GetNumberCode(DateTime.Now.Hour), GetNumberCode(DateTime.Now.Minute));
        }


        private string GetNumberCode(int number)
        {
            if (number < 10)
                return string.Format("0{0}", number);
            else
                return number.ToString();
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
                Backup backup = new Backup();

                var query = from e in XElement.Load("Databases.xml").Elements("BackupDatabase")
                            select new BackupDatabase
                            {
                                BackupPath = (string)e.Element("BackupPath"),
                                ServerName = (string)e.Element("ServerName"),
                                DBName = (string)e.Element("DBName"),
                                UserName = (string)e.Element("UserName"),
                                Password = (string)e.Element("Password")
                            };

                List<BackupDatabase> list = query.ToList();
                foreach (BackupDatabase dbToBackup in list)
                {
                    //BackupDatabase dbToBackup = list[0];

                    try
                    {
                        string backupPath = dbToBackup.BackupPath;
                        string backupFileName = string.Format("{0}_DB_{1}.bak", dbToBackup.DBName, dateTimePart);
                        string backupDestination = string.Format("{0}{1}", backupPath, backupFileName);

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
                        sqlServer.ConnectionContext.Connect();

                        using (EventLog elog = new EventLog("Application"))
                        {
                            message = "Connected successfully to " + dbToBackup.ServerName;
                            elog.Source = "Application";
                            elog.WriteEntry(message, EventLogEntryType.Information, 101, 1);
                        }

                        Database db = sqlServer.Databases[dbToBackup.DBName];

                        backup.Initialize = true;
                        backup.Checksum = true;
                        backup.ContinueAfterError = true;
                        backup.Devices.Add(deviceItem);

                        backup.Incremental = false; // set to be full database backup
                        backup.ExpirationDate = DateTime.Today.AddDays(180);
                        backup.LogTruncation = BackupTruncateLogType.Truncate; // log must be truncated after the backup is complete
                        backup.FormatMedia = false;

                        backup.SqlBackup(sqlServer);
                        backup.Devices.Remove(deviceItem);


                        //server.LoginSecure = true;
                        //server.Connect(dbToBackup.ServerName, dbToBackup.UserName, dbToBackup.Password);

                        //backup.Database = dbToBackup.DBName;
                        //backup.Initialize = true;
                        //backup.Files = backupPath + backupFileName;
                        //backup.Action = SQLDMO_BACKUP_TYPE.SQLDMOBackup_Database;
                        //backup.SQLBackup(server);
                        //server.DisConnect();

                        string messageTitle = string.Format("{0} Backup Tool", dbToBackup.DBName);
                        message = string.Format("Backup has been taken successfully into the file: {0}{1}", backupPath, backupFileName);
                        //EventLog.WriteEntry(messageTitle, message, EventLogEntryType.Information);

                        using (EventLog elog = new EventLog("Application"))
                        {
                            elog.Source = "Application";
                            elog.WriteEntry(message, EventLogEntryType.Information, 101, 1);
                        }
                    }
                    catch (Exception ex)
                    {
                        using (EventLog elog = new EventLog("Application"))
                        {
                            message = string.Format("Error in Database Backup Tool For Database: {0} -- {1}", dbToBackup.DBName, ex.Message);

                            elog.Source = "Application";
                            elog.WriteEntry(message, EventLogEntryType.Warning, 101, 1);
                        }

                        //EventLog.WriteEntry("Error in Database Backup Tool For Database: " + dbToBackup.DBName, ex.Message, EventLogEntryType.Warning);
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                using (EventLog elog = new EventLog("Application"))
                {
                    message = string.Format("Error in Database Backup Tool  {0}", ex.Message);

                    elog.Source = "Application";
                    elog.WriteEntry(message, EventLogEntryType.Warning, 101, 1);
                }

                //EventLog.WriteEntry("Error in Database Backup Tool", ex.Message, EventLogEntryType.Warning);
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
