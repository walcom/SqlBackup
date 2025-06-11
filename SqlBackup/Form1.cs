using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using SQLDMO;

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
            try
            {
                SQLDMO.SQLServer server = new SQLServer();
                Backup backup = new Backup();

                var query = from e in XElement.Load("Databases.xml").Elements("BackupDatabase")
                            select new BackupDatabase
                            {
                                BackupPath=(string)e.Element("BackupPath"),
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

                        server.LoginSecure = true;
                        server.Connect(dbToBackup.ServerName, dbToBackup.UserName, dbToBackup.Password);

                        backup.Database = dbToBackup.DBName;
                        backup.Initialize = true;
                        backup.Files = backupPath + backupFileName;
                        backup.Action = SQLDMO_BACKUP_TYPE.SQLDMOBackup_Database;
                        backup.SQLBackup(server);
                        server.DisConnect();

                        string messageTitle = string.Format("{0} Backup Tool", dbToBackup.DBName);
                        string message = string.Format("Backup has been taken successfully into the file: {0}{1}", backupPath, backupFileName);
                        EventLog.WriteEntry(messageTitle, message, EventLogEntryType.Information);
                    }
                    catch (Exception ex)
                    {
                        EventLog.WriteEntry("Error in Database Backup Tool For Database: " + dbToBackup.DBName, ex.Message, EventLogEntryType.Warning);
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Error in Database Backup Tool", ex.Message, EventLogEntryType.Warning);
            }
        }




        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                //BackupSenatesDB();
                //BackupHrisDB();
                //BackupHallsMgmtSystemDB();
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
