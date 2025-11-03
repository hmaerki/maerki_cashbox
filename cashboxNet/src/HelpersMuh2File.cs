using System;
using System.Collections.Generic;
using System.IO;

namespace cashboxNet
{
    #region Helpers
    public class LineFehlerException : CashboxException
    {
        public LineFehlerException(string message) : base(message)
        {
        }
    }

    public class MuhFile
    {
        private const string DIRECTORY_BACKUP = "out_backup";
        public const string FILENAME = "cashbox_journal.muh2";
        private const string FILENAME_OUT_TMP = "cashbox_journal_tmp.muh2";
        private static string TIMEFORMAT_BACKUP = "yyyy-MM-dd_HH-mm-ss";
        private string FILENAME_BACKUP
        {
            get
            {
                string timestamp = DateTime.Now.ToString(TIMEFORMAT_BACKUP);
                return $"cashbox_journal_{timestamp}.muh2";
            }
        }

        private string Filecontents;
        private Configuration config;

        public MuhFile(Configuration config_)
        {
            config = config_;
            string filename = Path.Combine(config.ProgramArguments.Directory, FILENAME);

            config.SetMuhFileTimeStamp(File.GetLastWriteTime(filename));

            Filecontents = ReadNotLocking(filename);
        }

        public IEnumerable<string> Readlines()
        {
            return Readlines(Filecontents);
        }

        public void Write(string newFilecontents)
        {
            if (config.ProgramArguments.KeepMuhFile)
            {
                // In debug-Mode we write a temporary file
                File.WriteAllText(MuhFile.FILENAME_OUT_TMP, newFilecontents);
                return;
            }
            if (File.Exists(MuhFile.FILENAME_OUT_TMP))
            {
                // Delete the temporary file if not used
                File.Delete(MuhFile.FILENAME_OUT_TMP);
            }

            if (Filecontents == newFilecontents)
            {
                // No change: Don't write to avoid the timestamp to change
                return;
            }

            // Create Backup-Directory
            if (!Directory.Exists(DIRECTORY_BACKUP))
            {
                Directory.CreateDirectory(DIRECTORY_BACKUP);
            }

            // Write Backup-File
            File.WriteAllText(Path.Combine(DIRECTORY_BACKUP, FILENAME_BACKUP), Filecontents);

            // Write File
            File.WriteAllText(FILENAME, newFilecontents);
        }
        private static IEnumerable<string> Readlines(string s)
        {
            return Readlines(new StringReader(s));
        }

        private static IEnumerable<string> Readlines(TextReader tr)
        {
            while (true)
            {
                string line = tr.ReadLine();
                if (line == null)
                {
                    yield break;
                }
                yield return line;
            }
        }

        private static string ReadNotLocking(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (StreamReader sr = new StreamReader(fs))
                {
                    return sr.ReadToEnd();
                }
            }
        }
    }
    #endregion

}
