using System.Collections.Generic;

namespace cashboxNet
{
  #region Index
  public enum EnumAnchors { TOP, BUCHUNGSVORLAGEN, ERFOLGSRECHNUNG, EROEFFUNGSBILANZ, SCHLUSSBILANZ };

  public class HtmlIndexRenderer
  {
    public static void Render(HtmlStreamWriter html, IEnumerable<BookkeepingAccount> accounts)
    {
      HtmlTag div = html.Body.Append("div", "class='div_description'");
      {
        div.Append("p", text: "Übersicht");
        HtmlTag ul = div.Append("ul");
        AppendLink(ul, EnumAnchors.BUCHUNGSVORLAGEN, "Buchungsvorlagen");
        AppendLink(ul, EnumAnchors.ERFOLGSRECHNUNG, "Abschluss: Erfolgsrechnung");
        AppendLink(ul, EnumAnchors.EROEFFUNGSBILANZ, "Abschluss: Eröffnungsbilanz");
        AppendLink(ul, EnumAnchors.SCHLUSSBILANZ, "Abschluss: Schlussbilanz");
      }

      {
        div.Append("p", text: "Konten");
        HtmlTag ul = div.Append("ul");
        foreach (BookkeepingAccount account in accounts)
        {
          if (account.HasEntries)
          {
            Konto konto = account.Konto;
            AppendLink(ul, Anchor(konto), $"{konto.KontoNr} {konto.Text}");
            /*
            HtmlTag li = ul.Append("li");
            li.Append("a", $"href='#{Anchor(konto)}'", $"{konto.KontoNr} {konto.Text}");
            */
          }
        }
      }
    }

    public static void AppendLink(HtmlTag ul, EnumAnchors anchor, string text)
    {
      AppendLink(ul, anchor.ToString(), text);
    }

    public static void AppendLink(HtmlTag ul, string anchor, string text)
    {
      HtmlTag li = ul.Append("li");
      li.Append("a", $"href='#{anchor}'", text);
    }

    public static string Anchor(Konto konto)
    {
      return $"konto_{konto.KontoNr}";
    }
  }
  #endregion

  #region Book/Account
  public class HtmlRendererAccount : IRendererAccount
  {
    private Configuration config;
    private HtmlStreamWriter html;
    private HtmlTag table;
    public BookkeepingAccount Account { get; }
    private const string ALIGN_CENTER = "align='right'";
    private const string ALIGN_RIGHT = "align='right'";
    private const string HTML_SYTLE_RED = "style='background:#f5b0b0'";
    private const string HTML_STYLE_BLUE = "style='background:#CCCCFF'";

    public HtmlRendererAccount(Configuration config_, HtmlStreamWriter html_, BookkeepingAccount account)
    {
      config = config_;
      html = html_;
      Account = account;

      AddTitle();

      table = html.Body.Append("table", "class='values'");
      AddHeader();
      AddRowEntry(config.DateStartValuta, N.F(account.Konto.EroeffnungsKontostand));
    }

    private void AddTitle()
    {
      HtmlTag p = html.Body.Append("p");
      p.Append("a", $"style='padding-left: 20px;' class='top' name='{HtmlIndexRenderer.Anchor(Account.Konto)}' href='#{EnumAnchors.TOP}'", "(Top)");
      html.Body.Append("h2", text: $"{Account.Konto.KontoNr}: {Account.Konto.Text}");
    }

    private void AddHeader()
    {
      HtmlTag tr = table.Append("tr");
      tr.Append("th", text: "Referenz");
      tr.Append("th", text: "Buchungstext");
      tr.Append("th", textRaw: "Gegen-<br/>konto", args: ALIGN_CENTER);
      tr.Append("th", text: "Soll", args: ALIGN_RIGHT);
      tr.Append("th", text: "Haben", args: ALIGN_RIGHT);
      tr.Append("th", text: "Saldo", args: ALIGN_RIGHT);
    }

    private void AddRowEntry(string date, string saldo)
    {
      HtmlTag tr = table.Append("tr");
      tr.Append("td").Append("dfn", text: date);
      tr.Append("td");
      tr.Append("td");
      tr.Append("td");
      tr.Append("td");
      tr.Append("td", args: ALIGN_RIGHT).Append("dfn", text: saldo);
    }

    public void WriteEntry(AccountEntry entry, KontoDay day)
    {
      string betrag = N.F(entry.Betrag);

      string color = "";
      if (entry.Entry.Verb == Entry.EnumVerb.BUCHUNGSVORSCHLAG) { color = HTML_STYLE_BLUE; }
      if (!entry.Entry.MessagesTodo.Empty) { color = HTML_STYLE_BLUE; }

      HtmlTag tr = table.Append("tr", args: color);

      // Referenz
      tr.Append("td").Append("dfn", text: entry.Referenz);
      {
        // Buchungstext
        HtmlTag td = tr.Append("td");
        td.Append("a", args: $"name='{entry.HtmlAnchor}'");
        td.Append("i", text: $"{entry.Entry.Buchungsvorlage.VorlageText} {entry.Entry.Buchungsvorlage.BuchungsText}");
        if (!string.IsNullOrEmpty(entry.Entry.Kommentar))
        {
          td.Append(text: " " + entry.Entry.Kommentar);
        }
        if (entry.MwstEntry != null)
        {
          td.Append(text: " ");
          AccountEntry mwstEntry = entry.MwstEntry;
          td.Append("a", args: $"href='#{mwstEntry.HtmlAnchor}'").Append("code", text: $"{entry.Entry.MWST.Tag} {N.F(mwstEntry.Betrag)}");
        }
        else
        {
          if (entry.GegenEntry.MwstEntry != null)
          {
            td.Append(text: " ");
            AccountEntry mwstEntry = entry.GegenEntry.MwstEntry;
            td.Append("a", args: $"href='#{mwstEntry.HtmlAnchor}'").Append("code", text: $"{entry.Entry.MWST.Tag} {N.F(mwstEntry.Betrag)}");
          }
        }
        if (entry.Entry.BankEntry != null)
        {
          td.Append("br");
          td.Append("dfn", text: entry.Entry.BankEntry.BankBuchungstext);
        }
        td.Append("br");
        if (entry.Entry.Verb == Entry.EnumVerb.FILE_BUCHUNG)
        {
          // Aus Filesystem
          td.Append("a", args: $"target='filebuchung' href='{BuchungenDirectory.SUBDIRECTORY}/{entry.Entry.Line}'").Append("code", text: entry.Entry.Line);
        }
        else
        {
          // Aus Muh2-File
          td.Append("code", text: entry.Entry.Line);
        }

        foreach (string text in entry.Entry.MessagesErrors)
        {
          td.Append("br");
          td.Append("dfn", args: "style='color:red'", text: text);
        }

        foreach (string text in entry.Entry.MessagesTodo)
        {
          td.Append("br");
          td.Append("dfn", args: "style='color:blue'", text: text);
        }

      }
      // Gegenkonto
      tr.Append("td", args: ALIGN_CENTER).Append("a", $"href='#{entry.GegenEntry.HtmlAnchor}'").Append("dfn", text: entry.GegenEntry.Konto.KontoNr.ToString()); ;
      // Soll
      tr.Append("td", args: ALIGN_RIGHT, text: (entry.Relation == BookkeepingRelation.SOLL) ? betrag : "");
      // Haben
      tr.Append("td", args: ALIGN_RIGHT, text: (entry.Relation == BookkeepingRelation.HABEN) ? betrag : "");
      // Saldo
      if (day != null)
      {
        tr.Append("td", args: ALIGN_RIGHT).Append("dfn", text: N.F(day.Saldo));
      }
    }

    public void WriteEndOfAccount(decimal saldo)
    {
    }
  }

  public class HtmlRendererBook : IRendererBook
  {
    private HtmlStreamWriter html;
    private Configuration config;

    public HtmlRendererBook(Configuration config_, HtmlStreamWriter html_)
    {
      config = config_;
      html = html_;
    }

    public IRendererAccount CreateAccountRenderer(BookkeepingAccount account)
    {
      return new HtmlRendererAccount(config, html, account);
    }
  }
  #endregion

  #region Closing
  /// <summary>
  /// Renders a Closing (Abschluss)
  /// </summary>
  public class HtmlClosingRenderer : IRendererClosing
  {
    HtmlStreamWriter html;
    public IClosingSubRenderer SubRenderer { get; private set; }

    public HtmlClosingRenderer(HtmlStreamWriter html_)
    {
      html = html_;
    }

    public void NewPage(EnumAnchors anchor, string title)
    {
      html.Body.Append("a", $"name='{anchor}'");
      html.Body.Append("h2", text: title);
      HtmlTag table = html.Body.Append("table", args: "class='abrechnung' xmlns=''");
      SubRenderer = new HtmlClosingSubRenderer(table);
    }
  }

  /// <summary>
  /// Renders a Section (Einnahmen/Ausgaben/Aktiven/Passiven) within the Closing (Abschluss)
  /// </summary>
  public class HtmlClosingSubRenderer : IClosingSubRenderer
  {
    private HtmlTag table;

    public HtmlClosingSubRenderer(HtmlTag table_)
    {
      table = table_;
    }

    public void SectionBegin(string sectionTitle)
    {
      HtmlTag tr = table.Append("tr");
      tr.Append("td", args: "class='title' colspan='4'", text: sectionTitle);
    }

    public void SectionEnd(string sectionTitle, decimal total)
    {
      HtmlTag tr = table.Append("tr");
      tr.Append("td", args: "class='title' colspan='3'", text: $"Total {sectionTitle}");
      tr.Append("td", args: "class='title' align='right'", text: N.F(total));

      WriteEmtyRow();
    }

    public void TitleStart(string title)
    {
      HtmlTag tr = table.Append("tr");
      tr.Append("td", args: "class='subtitle' colspan='4'", text: title);
    }

    public void TitleEnd(SubSection subTitle)
    {
      HtmlTag tr = table.Append("tr");
      tr.Append("td", args: "class='subtitle' colspan='2'", text: $"Total {subTitle.Title}");
      tr.Append("td");
      tr.Append("td", args: "class='subtitle' align='right'", text: N.F(subTitle.Saldo));
      WriteEmtyRow();
    }

    public void WriteLine(BookkeepingAccount account, decimal betrag)
    {
      HtmlTag tr = table.Append("tr");
      tr.Append("td", text: account.Konto.KontoNr.ToString());
      tr.Append("td", text: account.Konto.Text);
      tr.Append("td", args: "align='right'", text: N.F(betrag));
    }

    private void WriteEmtyRow()
    {
      HtmlTag tr2 = table.Append("tr");
      tr2.Append("td", textRaw: "&nbsp;");
    }
  }
  #endregion
}

