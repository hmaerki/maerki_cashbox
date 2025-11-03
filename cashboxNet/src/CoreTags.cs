using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace cashboxNet
{
  /// <summary>
  /// This class writes 'out_tags.csv'.
  /// The file may be interpreted using 'cashbox_tags-vorlage.xlsm'. 
  /// </summary>
  public class TagFile
  {
    #region Public
    public void Push(Journal journal)
    {
      journal.Loop(processEntry: WriteEntry);
    }

    public void Write(string filename)
    {
      using (StreamWriter fs = new StreamWriter(filename))
      {
        TagGroup.Write(fs, tags);
      }
    }
    #endregion

    #region Private
    private Dictionary<string, TagGroup> tags = new Dictionary<string, TagGroup>();
    private void WriteEntry(Entry entry)
    {
      foreach (string tag in entry.Tags)
      {
        TagGroup tagGroup;
        if (!tags.TryGetValue(tag, out tagGroup))
        {
          tagGroup = new TagGroup(tag);
          tags.Add(tag, tagGroup);
        }
        tagGroup.Add(entry);
      }
    }

    #region class TagGroup
    private class TagGroup
    {
      public string Tag;
      private List<string> lines = new List<string>();
      private decimal Sum = 0;
      private static string[] columns = { "TAG", "Referenz", "Valuta", "Betrag", "KontoHaben", "KontoSoll", "Vorlage", "Buchungstext", "AlleTags", "BankBuchungstext" };
      public TagGroup(string tag)
      {
        Tag = tag;
      }
      public void Add(Entry entry)
      {
        Sum += entry.Betrag;

        List<string> csv = new List<string>();
        csv.Add(Tag);
        csv.Add(entry.Referenz);
        csv.Add(entry.Valuta);
        csv.Add(entry.Betrag.ToString());
        csv.Add(entry.KontoHaben.KontoNr.ToString());
        csv.Add(entry.KontoSoll.KontoNr.ToString());
        csv.Add(entry.Buchungsvorlage.VorlageText);
        csv.Add(entry.Kommentar);
        csv.Add(string.Join("-", entry.Tags));
        csv.Add(entry.BankEntry != null ? entry.BankEntry.BankBuchungstext : "");

        Trace.Assert(columns.Length == csv.Count);
        lines.Add(string.Join("\t", csv));
      }

      public void WriteLines(TextWriter fs)
      {
        foreach (string line in lines.OrderBy(l => l))
        {
          fs.WriteLine(line);
        }
        fs.WriteLine($"Summe\t\t\t{Sum}");
      }

      public static void Write(TextWriter fs, Dictionary<string, TagGroup> tags)
      {
        fs.WriteLine(string.Join("\t", TagGroup.columns));
        foreach (TagGroup tagGroup in tags.Values.OrderBy(t => t.Tag))
        {
          tagGroup.WriteLines(fs);
        }
      }
    }
  }
  #endregion
  #endregion
}
