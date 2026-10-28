To Create the setup project you will need this **Visual Studio InstallerProjects extension**
https://marketplace.visualstudio.com/items?itemName=VisualStudioClient.MicrosoftVisualStudio2022InstallerProjects

Before running this tool, in SqlBackup/bin/Debug/Databases.xml, change this settings for each database you want to backup:

    -BackupPath: D:\YourBackupPath\ 
	-FinalCompressingPath: D:\YourBackupCompressingPath\
	-ServerName: .\SqlServerName
	-DB Name: DBName
	-UserName: Username
	-Password: Password
	-Compressed: 1 to compress the backup file, 0 for no compression 
	-ServiceAccountName: Name of service account that SQL Server service use,
	for example: NT Service\MSSQL$SqlServerName 
	(can be found in services, right click on SQL Server service, 
	select properties, and then select Log On tab)

And then put the exe path in  **Windows Tasks Scheduler**.
