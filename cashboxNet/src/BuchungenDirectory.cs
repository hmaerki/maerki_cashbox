using System.IO;

namespace cashboxNet
{
  public class BuchungenDirectory
  {
    public const string SUBDIRECTORY = "buchungen";
    public const string SKIP = "SKIP";
    private Configuration config;

    public BuchungenDirectory(Configuration config)
    {
      this.config = config;
    }

    public void Update(Journal journal)
    {
      string directory = Path.Combine(config.ProgramArguments.Directory, SUBDIRECTORY);
      foreach (string filenameFull in Directory.GetFileSystemEntries(directory))
      {
        string filename = Path.GetFileName(filenameFull);
        if (filename == "thumbs.db")
        {
          continue;
        }

        try
        {
          if (filename.Contains(SKIP))
          {
            continue;
          }

          if (RegexBeginReferenzVerbRest.TryMatch(filename, out RegexBeginReferenzVerbRest regex))
          {
            switch (regex.Verb)
            {
              case Entry.EnumVerb.FILE_BUCHUNG:
                journal.AddEntry(new Entry(config, regex));
                continue;
              default:
                JournalDay jd = journal.JournalDays.GetDay(regex.Valuta);
                jd.MessagesErrors.Add($"{filename}: Nur '{RegexBeginReferenzVerbRest.VERB_BUCHUNG}' ist zulässig.");
                continue;
            }
          }
          journal.AddErrorLine($"File '{filename}' kann nicht zugeordnet werden! Verwende '{SKIP}' im Filennamen um das File zu ignorieren.");
        }
        catch (CashboxException ex)
        {
          journal.AddErrorLine(filename, ex, commentLine: true);
        }
      }
    }
  }
}
