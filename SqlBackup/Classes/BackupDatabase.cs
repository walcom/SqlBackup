namespace SqlBackup
{
    class BackupDatabase
    {
        public string BackupPath { get; set; }
        public string ServerName { get; set; }
        public string DBName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }

        public int Compressed { get; set; } = 0;
    }
}
