using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
#if NET_FORMS
using System.Windows.Forms;
#endif // NET_FORMS

namespace cashboxNet
{
    public class CashboxException : Exception
    {
        public string ConfigurationFile { get; set; }
        public string ConfigurationFileLineNr { get; set; }

        public CashboxException(string message, Exception innerException = null) : base(message, innerException)
        {
        }

        public override string Message
        {
            get
            {
                string message = "";
                if (ConfigurationFile != null)
                {
                    message = ConfigurationFile;
                }
                if (ConfigurationFileLineNr != null)
                {
                    message += $"({ConfigurationFileLineNr})";
                }
                if (message.Length > 0)
                {
                    message += ": ";
                }
                message += base.Message;
                return message;
            }
        }
    }

    public class Result
    {
        public Configuration config;
        public Journal journal;
    }

    /// <summary>
    /// The main program flow of 'CashboxNet'
    /// </summary>
    public class Core
    {
        public const string CONFIGUARTION_FILE_JAHR = "cashbox_config_jahr.cs";
        public const string CONFIGUARTION_FILE_VORLAGEBUCHUNGEN = "cashbox_config_vorlagebuchungen.cs";
        public const string CONFIGUARTION_FILE_VORSCHLAEGE = "cashbox_config_vorschlaege.cs";
        public const string CONFIGUARTION_FILE_KONTENPLAN = "cashbox_config_kontenplan.cs";
        public static string[] CONFIGURATION_FILES = { CONFIGUARTION_FILE_KONTENPLAN, CONFIGUARTION_FILE_VORLAGEBUCHUNGEN, CONFIGUARTION_FILE_VORSCHLAEGE, CONFIGUARTION_FILE_JAHR };

        //
        // Read configuration and consistency check
        //  - Initialize Journal
        // Backup cashbox.muh
        // Read cashbox.muh
        //  - Update Journal
        // Read folder 'buchungsfiles'
        //  - Update Journal
        // Read bankdaten
        //  - Update Journal
        //    - Add missing entries
        //    - Error if wrong saldo
        // Check Beleg-Folder
        //  - Add TODO-Entries into Journal
        // Run MWST
        //  - Update Journal
        // ==> Journal ready
        //
        // 
        public Result Run(ConfigurationProgramArguments args)
        {
            Console.WriteLine("Read configuration...");
            Configuration config = new Configuration(args);

            ConfigurationLoader<Configuration>.UpdateFromFiles(args.Directory, config, CONFIGURATION_FILES);
            config.Validate();

            Journal journal = new Journal(config);

            // Load the journal from the file
            Console.WriteLine("Read Muh-File...");
            journal.Muh2FileRead();

            // Read the buchen-directory into the journal
            Console.WriteLine("Do bookkeeping...");
            BuchungenDirectory buchungenDirectory = new BuchungenDirectory(config);
            buchungenDirectory.Update(journal);

            // Read the bank accounts into the journal
            List<BankreaderResult> bankreaderResults = config.CreateBankReaderResults();

            foreach (BankreaderResult bankreaderResult in bankreaderResults)
            {
                bankreaderResult.WriteSaldo(journal);
                bankreaderResult.AddBankreaderResult(journal);
                bankreaderResult.WriteMappingFile();
            }

            foreach (BankreaderResult bankreaderResult in bankreaderResults)
            {
                bankreaderResult.AddBuchungsvorschlaege(journal);
            }

            journal.ConstraintAbschlussBuchungen();

            // Update BookkeepingBook
            BookkeepingBook book = journal.CreateBook();

            foreach (BankreaderResult bankreaderResult in bankreaderResults)
            {
                bankreaderResult.ErrorIfWrongEndOfTheDaySaldo(journal);
            }

            config.ApplyBookkeepingConstraints(journal, book);

            book.MwstAbrechnung(journal);

            BookkeepingClosing erfolgsrechnung = book.CreateErfolgsrechnung();
            erfolgsrechnung.ValidateBalance(journal);

            if (args.CreateTxt)
            {
                Console.WriteLine("Write Txt...");
                book.WriteTxtDirectory();
                book.WriteTxt();
            }

            if (args.CreateTags)
            {
                journal.WriteTags();
            }

            if (args.CreateHtml)
            {
                Console.WriteLine("Write Html...");
                book.WriteHtml(erfolgsrechnung, journal);
            }

            if (args.CreatePdf)
            {
                Console.WriteLine("Write Pdf...");
                // book.WritePdf(); // Nur Konten: Dies wird nicht benötigt...
                erfolgsrechnung.WritePdf();
                book.WritePdf(erfolgsrechnung);
            }

            // Write the journal to the file
            Console.WriteLine("Write Muh-File...");
            journal.Muh2FileWrite();

            return new Result { config = config, journal = journal };
        }


        // https://blogs.msdn.microsoft.com/jmstall/2007/02/12/making-catch-rethrow-more-debuggable/
        [DebuggerNonUserCode()]
        public void RunWithExceptionHandler(ConfigurationProgramArguments arguments)
        {
            try
            {
                Run(arguments);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"---- Error {ex.GetType()}");
                if (ex is CashboxException exc)
                {
                    if (exc.ConfigurationFile != null)
                    {
                        Console.WriteLine($"{exc.ConfigurationFile}({exc.ConfigurationFileLineNr})");
                    }
                }
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
#if NET_FORMS
        DialogResult rc = MessageBox.Show(text: ex.Message, caption: "Cashbox-Fehler (Abbrechen-Button: Start Debugger)", buttons: MessageBoxButtons.OKCancel);
        if (rc == DialogResult.Cancel)
        {
          throw;
        }
#endif // NET_FORMS
            }
        }
    }


    /// <summary>
    /// VSB80/VSS25
    /// </summary>
    public class MwstSatz
    {
        public string Tag { get; private set; }
        public Decimal Value { get; private set; }
        public Konto Konto { get; private set; }
        public string Text { get; private set; }

        public MwstSatz(string tag, double mwst, Konto konto, string text)
        {
            Tag = tag;
            Value = new Decimal(mwst);
            Konto = konto;
            Text = text;
        }
    }

    /// <summary>
    /// A line in the muh2-file which could not be parsed.
    /// It will be stored apart from the meaningful-lines and printed as errors on top of the new file.
    /// </summary>
    public interface IError
    {
        void Write(TextWriter sw);
    }

    public class ErrorMessage : IError
    {
        private string message;
        public ErrorMessage(string message_)
        {
            message = message_;
        }

        public virtual void Write(TextWriter sw)
        {
            sw.WriteLine($"{RegexBeginBasic.VERB_FEHLER} {message}");
            return;
        }
    }

    public class ErrorException : IError
    {
        private string line;
        private Exception exception;
        private bool commentLine;
        public ErrorException(string line_, Exception exception_, bool commentLine_)
        {
            line = line_;
            exception = exception_;
            commentLine = commentLine_;
        }

        public void Write(TextWriter sw)
        {
            sw.WriteLine($"{RegexBeginBasic.VERB_FEHLER} Nächste Zeile: {exception.Message}");
            if (commentLine)
            {
                sw.Write($"{RegexBeginBasic.VERB_COMMENT} ");
            }
            sw.WriteLine(line);
        }
    }
}

