using System;
using CommandLine;
using PdfSharp.Fonts;

namespace cashboxNet
{
    class Program
    {
        static void Main(string[] args)
        {
            // Initialize PDFsharp font resolver for Linux compatibility
            // See https://docs.pdfsharp.net/link/font-resolving.html
            GlobalFontSettings.FontResolver = new SystemFontResolver();

            /*
            TestISO2022 t = new TestISO2022();
            t.TestBuildReferenceFiles();
            return;
            */

            Parser.Default.ParseArguments<CommandLineOptions>(args).WithParsed(options =>
                      {
                          if (options.Dir != null)
                          {
                            Environment.CurrentDirectory = options.Dir;
                          }

                          ConfigurationProgramArguments arguments = new ConfigurationProgramArguments(options, Environment.CurrentDirectory);

                          Core core = new Core();
                          core.RunWithExceptionHandler(arguments);
                      }
              );
        }

    }
}
