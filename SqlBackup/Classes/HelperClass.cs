using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace SqlBackup.Classes
{
    public class HelperClass
    {
        public static string GetNumberCode(int number)
        {
            return (number < 10) ? string.Format("0{0}", number) : number.ToString();
        }

        public static void CompressBackupFile(string sourceFile, string destinationFile, string serviceAccountName)
        {
            try
            {
                GrantReadAccess(sourceFile, serviceAccountName);

                using (FileStream sourceStream = File.OpenRead(sourceFile))
                using (FileStream destinationStream = File.Create(destinationFile))
                using (ZipArchive archive = new ZipArchive(destinationStream, ZipArchiveMode.Create))
                {
                    ZipArchiveEntry entry = archive.CreateEntry(Path.GetFileName(sourceFile), CompressionLevel.Optimal);

                    using (Stream entryStream = entry.Open())
                    {
                        sourceStream.CopyTo(entryStream);
                    }
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

        private static void GrantReadAccess(string filePath, string accountName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
                throw new ArgumentException("The service account name is required.", nameof(accountName));

            if (accountName.Contains("\""))
                throw new ArgumentException("The service account name contains an invalid character.", nameof(accountName));

            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "icacls.exe"),
                Arguments = string.Format("\"{0}\" /grant \"{1}:R\"", filePath, accountName),
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                    throw new InvalidOperationException("Failed to start icacls.exe.");

                process.WaitForExit();

                if (process.ExitCode != 0)
                    throw new InvalidOperationException(
                        string.Format("icacls.exe failed with exit code {0} for '{1}'.", process.ExitCode, filePath));
            }
        }


        public static void WriteInEventLog(string message, EventLogEntryType type)
        {
            using (EventLog elog = new EventLog("Application"))
            {
                elog.Source = "Application";
                elog.WriteEntry(message, type, 101, 1);
            }
        }
    }
}
