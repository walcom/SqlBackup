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

        public static void CompressBackupFile(string sourceFile, string destinationFile)
        {
            try
            {
                using (FileStream originalFileStream = File.OpenRead(sourceFile))
                using (FileStream compressedFileStream = File.Create(destinationFile))
                using (GZipStream compressionStream = new GZipStream(compressedFileStream, CompressionLevel.Optimal)) //Fastest 
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
