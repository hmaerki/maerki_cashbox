using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CSScriptLib;

namespace cashboxNet
{
    /// <summary>
    /// The interfaces which is implemented by the configuration-CSScript-files.
    /// </summary>
    public interface IUpdate<T>
    {
        void Update(T configuration);
    }

    /// <summary>
    /// Some magic to use c#-cs files as CSScript.
    /// The configuration is stored as CSScript.
    /// </summary>
    public class ConfigurationLoader<T> where T : class
    {
        public static void UpdateFromFiles(string directory, T config, string[] files)
        {
            // CSScript.EvaluatorConfig.Engine = EvaluatorEngine.CodeDom;
            // CSScript.EvaluatorConfig.Engine = EvaluatorEngine.Mono;
            CSScript.EvaluatorConfig.Engine = EvaluatorEngine.Roslyn;

            // DebugBuild will show the file and line in the stacktrace. This is important for good error-messages!
            CSScript.EvaluatorConfig.DebugBuild = true;

            string FileBeeingProcessed = null;
            try
            {
                foreach (string file in files)
                {
                    Console.WriteLine($"   {file}...");
                    string cs_file = Path.Combine(directory, file);
                    if (!File.Exists(cs_file))
                    {
                        throw new FileNotFoundException($"Configuration file not found: {file}", cs_file);
                    }
                    FileBeeingProcessed = file;
                    IUpdate<T> update = CSScript.Evaluator.LoadFile<IUpdate<T>>(cs_file);
                    update.Update(config);
                }
            }
            catch (CashboxException e)
            {
                /*
                bei cashboxNet.Configuration.AddMwstRelevanteAbschlussbuchung(String buchung) in D:\cashbox\branches\cashboxNet\cashboxNet\Configuration.cs:Zeile 311.
                bei UpdateVorlagebuchungen.Update(Configuration config) in c:\Users\maerki\AppData\Local\Temp\CSSCRIPT\dynamic\1484.9db21675-a4c4-4c5f-83aa-7620de6e88f3.tmp:Zeile 102.
                bei cashboxNet.ConfigurationLoader.UpdateFromFiles(String directory) in D:\cashbox\branches\cashboxNet\cashboxNet\Configuration.cs:Zeile 33.

                at cashboxNet.Configuration.SetBuchungsvorlageNichtGefunden(Int32 kontoNr, String buchungsvorlageCredit, String buchungsvorlageDebit, String konto_ersatz) in C:\Projekte\hans\cashbox_git\cashboxNet\src\Configuration.cs:line 447
                at UpdateVorlagebuchungen.Update(Configuration config) in c:\Users\maerki\AppData\Local\Temp\CSSCRIPT\dynamic\11208.06c85d02-29c1-470c-ac51-f85d954d0f72.tmp:line 133
                at cashboxNet.ConfigurationLoader`1.UpdateFromFiles(String directory, T config, String[] files) in C:\Projekte\hans\cashbox_git\cashboxNet\src\ConfigurationLoader.cs:line 62
                */
                e.ConfigurationFile = FileBeeingProcessed;
                Regex regexLine = new Regex(@"\.tmp:(Zeile|line) (?<lineNr>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                Match match = regexLine.Match(e.StackTrace);
                if (match.Success)
                {
                    string lineNr = match.Groups["lineNr"].Value;
                    e.ConfigurationFileLineNr = lineNr;
                }
                throw;
            }
            catch (CSScriptLib.CompilerException e)
            {
                string[] lines = e.Message.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                /*
                c:\Users\maerki\AppData\Local\Temp\CSSCRIPT\dynamic\12040.ec855d8d-69b1-4992-9fdf-9dd5ce35e055.tmp(21,11): error CS1061: 'cashboxNet.KontenplanHelper' enthält keine Definition für 'GroupBilanzAktiven', und es konnte keine Erweiterungsmethode 'GroupBilanzAktiven' gefunden werden, die ein erstes Argument vom Typ 'cashboxNet.KontenplanHelper' akzeptiert (Fehlt eine Using-Direktive oder ein Assemblyverweis?).
                c:\Users\maerki\AppData\Local\Temp\CSSCRIPT\dynamic\12040.ec855d8d-69b1-4992-9fdf-9dd5ce35e055.tmp(22,11): error CS1061: 'cashboxNet.KontenplanHelper' enthält keine Definition für 'GroupBilanz2', und es konnte keine Erweiterungsmethode 'GroupBilanz2' gefunden werden, die ein erstes Argument vom Typ 'cashboxNet.KontenplanHelper' akzeptiert (Fehlt eine Using-Direktive oder ein Assemblyverweis?).
                */
                Regex regexLine = new Regex(@"^(?<filename>.*?\.tmp)(?<rest>\([0-9,]+\):.*)$", RegexOptions.Compiled);
                IEnumerable<string> lines2 = lines.Select(delegate (string line)
                 {
                     Match match = regexLine.Match(line);
                     if (match.Success)
                     {
                         return FileBeeingProcessed + match.Groups["rest"].Value;
                     }

                     return line;
                 });
                string message = string.Join(Environment.NewLine, lines2);
                throw new CashboxException(message, e);
            }
        }
    }
}
