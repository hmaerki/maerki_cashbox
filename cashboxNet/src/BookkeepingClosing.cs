using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace cashboxNet
{
  /// <summary>
  /// Within Erfolgsrechnung/Bilanz: Umlaufvermögen, Anlagevermögen
  /// </summary>
  public class SubSection
  {
    public string Title { get; private set; }
    public Decimal Saldo { get; private set; }
    public SubSection(string title)
    {
      Saldo = 0;
      Title = title;
    }

    public void UpdateSaldo(decimal saldo)
    {
      Saldo += saldo;
    }
  }

  /// <summary>
  /// Interface to render Erfolgsrechnung/Bilanz
  /// </summary>
  public interface IRendererClosing
  {
    IClosingSubRenderer SubRenderer { get; }
    void NewPage(EnumAnchors anchor, string title);
  }

  /// <summary>
  /// Interface to render Aktiven, Passiven, Einnahmen, Ausgaben within Erfolgsrechnung/Bilanz
  /// </summary>
  public interface IClosingSubRenderer
  {
    void SectionBegin(string sectionTitle);
    void SectionEnd(string sectionTitle, decimal total);
    void TitleStart(string title);
    void WriteLine(BookkeepingAccount account, decimal betrag);
    void TitleEnd(SubSection subTitle);
  }

  /// <summary>
  /// Beschreibt eine Zeile in der Erfolgsrechnung / Bilanz
  /// </summary>
  public class ClosingLine
  {
    public readonly Konto konto = null;
    public readonly string title = null;
    public ClosingLine(Konto konto_)
    {
      konto = konto_;
    }
    public ClosingLine(string title_)
    {
      title = title_;
    }
  }

  /// <summary>
  /// Describes the structure of the closing (Ausgaben/Einnahmen/Aktiven/Passiven)
  /// </summary>
  public class ConfigurationClosingStructureSection
  {
    private List<ClosingLine> lines = new List<ClosingLine>();
    public IEnumerable<ClosingLine> Lines { get { return lines; } }
    public void Add(string name)
    {
      lines.Add(new ClosingLine(name));
    }
    public void Add(Konto konto)
    {
      lines.Add(new ClosingLine(konto));
    }
  }

  /// <summary>
  /// Describes the structure of the closing (Erfolgsrechnung/Bilanz)
  /// </summary>
  public class ConfigurationClosingStructure
  {
    public ConfigurationClosingStructureSection A = new ConfigurationClosingStructureSection();
    public ConfigurationClosingStructureSection B = new ConfigurationClosingStructureSection();
  }

  public class BookkeepingClosing
  {
    private Configuration config;
    private BookkeepingBook book;

    public BookkeepingClosing(Configuration config_, BookkeepingBook book_)
    {
      config = config_;
      book = book_;
    }

    public void WriteHtml(HtmlStreamWriter html)
    {
      HtmlClosingRenderer renderer = new HtmlClosingRenderer(html);
      WriteClosing(renderer, out Decimal differenceBalanceOpening, out Decimal differenceBalanceClosing);
    }

    public void WritePdf()
    {
      string filename = config.ProgramArguments.CreatePdfOrDelete("out_Abschluss.pdf");
      if (filename != null)
      {
        PdfDocument pdf = new PdfDocument(config);
        WritePdf(pdf);
        pdf.Write(filename);
      }
    }

    public void WritePdf(PdfDocument pdf)
    {
      PdfClosingRenderer renderer = new PdfClosingRenderer(config, pdf);
      WriteClosing(renderer, out Decimal differenceBalanceOpening, out Decimal differenceBalanceClosing);
    }

    /// <summary>
    /// Call a subrenderer for Erfolgsrechnung/Eröffnungsbilanz/Schlussbilanz
    /// This  'renderer' will transform the output into HTML or PDF.
    /// </summary>
    private void WriteClosing(IRendererClosing renderer, out Decimal differenceBalanceOpening, out Decimal differenceBalanceClosing)
    {
      renderer.NewPage(EnumAnchors.ERFOLGSRECHNUNG, $"Erfolgsrechnung {config.DatePeriod}");
      {
        WriteClosingSub(renderer.SubRenderer, config.ClosingStructureErfolgsrechnung.A, "Einnahmen");
        WriteClosingSub(renderer.SubRenderer, config.ClosingStructureErfolgsrechnung.B, "Ausgaben");
      }

      renderer.NewPage(EnumAnchors.EROEFFUNGSBILANZ, $"Eröffnungsbilanz vom {config.DateStartValuta}");
      {
        differenceBalanceOpening = WriteClosingSub(renderer.SubRenderer, config.ClosingStructureBilanz.A, "Aktiven", eroeffnung: true);
        differenceBalanceOpening -= WriteClosingSub(renderer.SubRenderer, config.ClosingStructureBilanz.B, "Passiven", eroeffnung: true);
      }

      renderer.NewPage(EnumAnchors.SCHLUSSBILANZ, $"Schlussbilanz vom {config.DateEndValuta}");
      {
        differenceBalanceClosing = WriteClosingSub(renderer.SubRenderer, config.ClosingStructureBilanz.A, "Aktiven", eroeffnung: false);
        differenceBalanceClosing -= WriteClosingSub(renderer.SubRenderer, config.ClosingStructureBilanz.B, "Passiven", eroeffnung: false);
      }
    }

    private class DummyClosingSubRenderer : IClosingSubRenderer
    {
      public void SectionBegin(string sectionTitle) { }
      public void SectionEnd(string sectionTitle, decimal total) { }
      public void TitleStart(string title) { }
      public void WriteLine(BookkeepingAccount account, decimal betrag) { }
      public void TitleEnd(SubSection subTitle) { }
    }

    private class DummyClosingRenderer : IRendererClosing
    {
      private static DummyClosingSubRenderer sub = new DummyClosingSubRenderer();
      public IClosingSubRenderer SubRenderer { get { return sub; } }
      public void NewPage(EnumAnchors anchor, string title) { }
    }

    public void ValidateBalance(Journal journal)
    {
      DummyClosingRenderer renderer = new DummyClosingRenderer();
      WriteClosing(renderer, out Decimal differenceBalanceOpening, out Decimal differenceBalanceClosing);
      ValidateBalance(journal, "Eröffnungsbilanz", differenceBalanceOpening);
      if (journal.GewinnBuchungEntry != null)
      {
        ValidateBalance(journal, "Schlussbilanz", differenceBalanceClosing);
      }
    }

    private void ValidateBalance(Journal journal, string text, Decimal balance)
    {
      if (balance != 0M)
      {
        journal.AddErrorLine($"Die {text} weist einen Fehler von {N.F(balance)} auf, sollte aber 0 sein!");
      }
    }

    /// <summary>
    /// According the structure of the closing (Bilanz, Erfolgsrechnung), this
    /// method calls the 'renderer' which then transforms the output into HTML or PDF.
    /// </summary>
    private Decimal WriteClosingSub(IClosingSubRenderer renderer, ConfigurationClosingStructureSection sub, string sectionTitle, bool eroeffnung = false)
    {
      renderer.SectionBegin(sectionTitle);
      SubSection subTitle = null;
      Decimal total = 0;
      foreach (ClosingLine line in sub.Lines)
      {
        if (line.title != null)
        {
          if (subTitle != null)
          {
            renderer.TitleEnd(subTitle);
          }
          renderer.TitleStart(line.title);

          subTitle = new SubSection(line.title);
          continue;
        }

        Trace.Assert(line.konto != null);
        BookkeepingAccount account = book[line.konto];
        decimal betrag = eroeffnung ? account.Konto.EroeffnungsKontostand : account.Saldo;
        if (betrag != 0M)
        {
          // Accounts with saldo 0 will be skipped.
          renderer.WriteLine(account, betrag);
          total += betrag;
        }

        if (subTitle != null)
        {
          subTitle.UpdateSaldo(betrag);
        }
      }

      if (subTitle != null)
      {
        renderer.TitleEnd(subTitle);
      }
      renderer.SectionEnd(sectionTitle, total);

      return total;
    }
  }
}
