using CommandLine;
using CommandLine.Text;

namespace cashboxNet
{
    /// <summary>
    /// https://commandline.codeplex.com/
    /// </summary>
    public class CommandLineOptions
    {
        public CommandLineOptions()
        {
            // Parameterless constructor for CommandLineParser compatibility
        }

        [Option("dir", Default = null, HelpText = "Instead of cwd, we provide the directory containing cashbox_journal.muh2.")]
        public string Dir { get; set; }

        [Option("txt", Default = false, HelpText = "Create txt-Ouput.")]
        public bool CreateTxt { get; set; }

        [Option("html", Default = false, HelpText = "Create html-Ouput.")]
        public bool CreateHtml { get; set; }

        [Option("pdf", Default = false, HelpText = "Create pdf-Ouput.")]
        public bool CreatePdf { get; set; }

        [Option("tags", Default = false, HelpText = "Create tags-Ouput.")]
        public bool CreateTags { get; set; }

        [Option("keep", Default = false, HelpText = "Write tmp-file: 'cashbox_journal_tmp.muh2'.")]
        public bool KeepMuhFile { get; set; }

        [Option('v', "verbose", Default = false, HelpText = "Prints all messages to standard output.")]
        public bool Verbose { get; set; }

        [Option("timestamp", Default = null, HelpText = "The timestamp is taken from the file if not specified here.")]
        public string FileVersionTimestamp { get; set; }
    }

}
