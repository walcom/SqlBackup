To Create the setup project you will need this **Visual Studio InstallerProjects extension**
https://marketplace.visualstudio.com/items?itemName=VisualStudioClient.MicrosoftVisualStudio2022InstallerProjects

Before running this tool, in SqlBackup/bin/Debug/Databases.xml, change this settings for each database you want to backup:
    -**BackupPath**: D:\YourBackupPath\ 
	-**FinalCompressingPath**: D:\YourBackupCompressingPath\
	-**ServerName**: .\SqlServerName
	-**DB Name**: DBName
	-**UserName**: User
	-**Password**: Password
	-**Compressed**: 1 to compress, 0 for no compression 


And then put the exe path in  **Windows Tasks Scheduler**.
