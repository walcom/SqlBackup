using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SqlBackup
{
    class BackupDatabase
    {
        public string BackupPath { get; set; }
        public string ServerName { get; set; }
        public string DBName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }

    }
}
