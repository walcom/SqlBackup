To Create the setup project you will need this **Visual Studio InstallerProjects extension **
https://marketplace.visualstudio.com/items?itemName=VisualStudioClient.MicrosoftVisualStudio2022InstallerProjects

Before running this tool, in SqlBackup/bin/Debug/Databases.xml, change this settings for each database you want to backup:
<BackupDatabase>
		<BackupPath>D:\YourBackupPath\ </BackupPath>
		 <FinalCompressingPath>D:\YourBackupCompressingPath\</FinalCompressingPath>
		 <ServerName>.\SqlServerName</ServerName>
		 <DBName>DBName</DBName>
		 <UserName>User</UserName> 
		 <Password>Password</Password> 
		<Compressed>1</Compressed> <!-- 1 to compress, 0 for no compression -->
	</BackupDatabase>

And then put the exe path in** Windows Tasks Scheduler.**
