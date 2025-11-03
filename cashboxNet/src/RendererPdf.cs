using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

/// <summary>
/// http://www.pdfsharp.net/wiki/MigraDocSamples.ashx
/// http://www.pdfsharp.net/wiki/Invoice-sample.ashx
/// 
/// https://github.com/empira/PDFsharp/ 
/// https://github.com/empira/MigraDoc/
/// 
/// Fast table rendering in MigraDoc
/// http://forum.pdfsharp.net/viewtopic.php?f=2&t=679
/// </summary>
namespace cashboxNet
{
    #region FontResolver

    /// <summary>
    /// Font resolver for PDFsharp 6.x that uses fonts from resources/fonts directory.
    /// See https://docs.pdfsharp.net/link/font-resolving.html
    /// </summary>
    public class SystemFontResolver : IFontResolver
    {
        private readonly Dictionary<string, byte[]> _fontBytes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        // Map font names to the actual files in resources/fonts
        private readonly Dictionary<string, string> _fontFiles = new Dictionary<string, string>{
      { "Helvetica", "Helvetica.ttf" },
      { "Helvetica-Bold", "Helvetica-Bold.ttf" },
      { "Helvetica-Oblique", "Helvetica-Oblique.ttf" },
      { "Helvetica-BoldOblique", "Helvetica-BoldOblique.ttf" },
      { "Helvetica-Black", "Helvetica-Black.ttf" },
      
      // Add alternative names for compatibility
      { "Helvetica-Italic", "Helvetica-Oblique.ttf" },
      { "Helvetica-BoldItalic", "Helvetica-BoldOblique.ttf" },

      { "Courier New", "cour.ttf" },
      { "Courier New-Italic", "couri.ttf" },
      { "Courier New-Bold", "courbd.ttf" },
      { "Courier New-BoldItalique", "courbi.ttf" },
    };

        public SystemFontResolver()
        {
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            // Build the font name based on style
            string fontKey = familyName;

            if (isBold && isItalic)
                fontKey = $"{familyName}-BoldOblique";
            else if (isBold)
                fontKey = $"{familyName}-Bold";
            else if (isItalic)
                fontKey = $"{familyName}-Oblique";

            // Try exact match first
            if (_fontFiles.ContainsKey(fontKey))
            {
                return new FontResolverInfo(fontKey);
            }

            // Try base font name
            if (_fontFiles.ContainsKey(familyName))
            {
                return new FontResolverInfo(familyName);
            }

            // Fallback to regular Helvetica
            if (_fontFiles.ContainsKey("Helvetica"))
            {
                Console.WriteLine($"Warning: Font '{familyName}' (bold={isBold}, italic={isItalic}) not found, using 'Helvetica' as fallback");
                return new FontResolverInfo("Helvetica");
            }

            throw new InvalidOperationException(
              $"Font '{familyName}' cannot be resolved and no fallback font available.\n" +
              // $"Font directory: {fontDirectory}\n" +
              $"Available fonts: {string.Join(", ", _fontFiles.Keys)}");
        }

        public byte[] GetFont(string faceName)
        {
            if (!_fontFiles.TryGetValue(faceName, out string fontPath))
            {
                throw new InvalidOperationException($"{fontPath}: Font file for '{faceName}' not found.");
            }
            string fontResource = $"cashboxNet.resources.fonts.{fontPath}";
            if (_fontBytes.TryGetValue(fontPath, out byte[] fontBytes))
            {
                return fontBytes;
            }
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(fontResource);
            if (stream == null)
            {
                throw new InvalidOperationException($"{fontResource}: Font resource for '{faceName}' not found.");
            }
            fontBytes = new byte[stream.Length];
            int readLength = stream.Read(fontBytes, 0, fontBytes.Length);
            Debug.Assert(readLength == stream.Length);
            _fontBytes[fontPath] = fontBytes;

            return fontBytes;
        }
    }
    #endregion

    #region PdfDocument
    public class PdfDocument
    {
        private readonly static byte TableBorderBrightness = 242;
        public static Color TableBorderColor = new Color(TableBorderBrightness, TableBorderBrightness, TableBorderBrightness); // new Color(242, 242, 242);

        private readonly Configuration config;
        private readonly Document document;
        private readonly Section section;
        public Unit PageWidthWithoutMargins { get; private set; }

        public PdfDocument(Configuration config_)
        {
            config = config_;

            document = SetupDocument(config);

            // Each MigraDoc document needs at least one section.
            section = SetupSection(document);

            PageWidthWithoutMargins = section.PageSetup.PageWidth - section.PageSetup.LeftMargin - section.PageSetup.RightMargin;

            DefineStyles();
            // Table 'Closing'
            PdfClosingTable.DefineStyles(document);
            // Table 'Account'
            PdfAccountTable.DefineStyles(document);
            AddHeader();
            AddFooter();
        }

        private static Document SetupDocument(Configuration config)
        {
            Document document = new Document();
            document.Info.Title = config.Title;
            document.Info.Subject = config.Mandant;
            document.Info.Author = "CashboxNet";
            return document;
        }

        private static Section SetupSection(Document document)
        {
            Section section = document.AddSection();

            PageSetup pageSetup = document.DefaultPageSetup.Clone();
            pageSetup.LeftMargin = "3cm";
            pageSetup.RightMargin = "2.5cm";
            pageSetup.TopMargin = "2.8cm";
            pageSetup.BottomMargin = "2.8cm";
            pageSetup.HeaderDistance = "1.5cm";
            pageSetup.FooterDistance = "1.5cm";
            section.PageSetup = pageSetup;

            return section;
        }

        private void DefineStyles()
        {
            // Get the predefined style Normal.
            Style style = document.Styles[StyleNames.Normal];

            // Because all styles are derived from Normal, the next line changes the 
            // font of the whole document. Or, more exactly, it changes the font of
            // all styles and paragraphs that do not redefine the font.
            style.Font.Name = "Helvetica";
            style.Font.Size = 10;

            style = this.document.Styles[StyleNames.Header];
            style.BaseStyle = StyleNames.Normal;
            style.ParagraphFormat.AddTabStop(PageWidthWithoutMargins / 2, TabAlignment.Center);
            style.ParagraphFormat.AddTabStop(PageWidthWithoutMargins, TabAlignment.Right);

            style = this.document.Styles[StyleNames.Footer];
            style.BaseStyle = StyleNames.Normal;
            style.ParagraphFormat.AddTabStop(PageWidthWithoutMargins / 2, TabAlignment.Center);
            style.ParagraphFormat.AddTabStop(PageWidthWithoutMargins, TabAlignment.Right);

            style = this.document.Styles[StyleNames.Heading3];
            style.BaseStyle = StyleNames.Normal;
            style.ParagraphFormat.Font.Size = 10;
            style.ParagraphFormat.Font.Bold = true;
            style.ParagraphFormat.KeepWithNext = true;

            style = this.document.Styles[StyleNames.Heading2];
            style.BaseStyle = StyleNames.Heading3;
            style.ParagraphFormat.SpaceBefore = "6mm";
            style.ParagraphFormat.SpaceAfter = "2mm";
            style.ParagraphFormat.Font.Size = 12;
            style.ParagraphFormat.AddTabStop(PageWidthWithoutMargins, TabAlignment.Right);

            style = this.document.Styles[StyleNames.Heading1];
            style.BaseStyle = StyleNames.Heading2;
            style.ParagraphFormat.SpaceBefore = "0mm";
            style.ParagraphFormat.SpaceAfter = "3mm";
            style.ParagraphFormat.Font.Size = 16;
            style.ParagraphFormat.PageBreakBefore = true;
        }

        private void AddHeader()
        {
            // Create header
            Paragraph paragraph = section.Headers.Primary.AddParagraph();
            // LEFT
            paragraph.AddText($"Buchhaltung {config.Jahr}");

            // CENTER
            paragraph.AddTab();
            paragraph.AddFormattedText(config.Mandant, TextFormat.Bold);

            // RIGHT
            paragraph.AddTab();
            paragraph.AddPageField();
            paragraph.AddText(" (");
            paragraph.AddNumPagesField();
            paragraph.AddText(")");
        }

        private void AddFooter()
        {
            // Create footer
            Paragraph paragraph = section.Footers.Primary.AddParagraph();
            // LEFT
            paragraph.AddText($"{MuhFile.FILENAME}: {config.MuhFileTimeStamp}");

            // CENTER
            paragraph.AddTab();

            // RIGHT
            paragraph.AddTab();
            paragraph.AddText($"Report: {config.NowTimeStamp}");
        }

        public void AddH1(string title)
        {
            Paragraph paragraph = section.AddParagraph(title, StyleNames.Heading1);
        }

        public void AddH2(string title, string total = null)
        {
            Paragraph paragraph = section.AddParagraph(title, StyleNames.Heading2);
            // Set the OutlineLevel to BodyText to avoid bookmarks for headings:
            // https://stackoverflow.com/questions/8689469/migradoc-pdfsharp-paragraph-without-associated-bookmarks
            paragraph.Format.OutlineLevel = OutlineLevel.BodyText;
            if (total != null)
            {
                paragraph.AddTab();
                paragraph.AddText(total);
            }
        }

        public Table AddTable()
        {
            return section.AddTable();
        }


        public void Write(string filename)
        {
            // Create a renderer for the MigraDoc document.
            PdfDocumentRenderer pdfRenderer = new PdfDocumentRenderer();
            // Associate the MigraDoc document with a renderer
            pdfRenderer.Document = document;

            // Layout and render document to PDF
            pdfRenderer.RenderDocument();

            // Save the document...
            pdfRenderer.PdfDocument.Save(filename);

            // ...and start a viewer.
            // Process.Start(filename);
        }
    }
    #endregion

    #region Render Closing (Abschluss: Erfolgsrechnung/Bilanz)

    /// <summary>
    /// Renders a Closing (Abschluss)
    /// </summary>
    public class PdfClosingRenderer : IRendererClosing
    {
        private PdfDocument pdf;
        private Configuration config;
        public IClosingSubRenderer SubRenderer { get; private set; }

        public PdfClosingRenderer(Configuration config_, PdfDocument pdf_)
        {
            this.config = config_;
            pdf = pdf_;
            SubRenderer = new PdfClosingSubRenderer(pdf);
        }

        public void NewPage(EnumAnchors anchor, string title)
        {
            pdf.AddH1(title);
        }
    }

    /// <summary>
    /// Renders a Section (Einnahmen/Ausgaben/Aktiven/Passiven) within the Closing (Abschluss)
    /// </summary>
    public class PdfClosingSubRenderer : IClosingSubRenderer
    {
        private PdfDocument pdf;
        private PdfClosingTable table = null;
        private bool addEmptyRow = false;

        public PdfClosingSubRenderer(PdfDocument pdf_)
        {
            pdf = pdf_;
        }

        public void SectionBegin(string sectionTitle)
        {
            pdf.AddH2(sectionTitle);

            Trace.Assert(table == null);
            table = new PdfClosingTable(pdf);
        }

        public void SectionEnd(string sectionTitle, decimal total)
        {
            Trace.Assert(table != null);
            table = null;

            pdf.AddH2($"Total {sectionTitle}", N.F(total));
        }

        public void TitleStart(string title)
        {
            if (addEmptyRow)
            {
                table.AddRowEmpty();
                addEmptyRow = false;
            }
            table.AddRowTitle(title, "", borderVisible: false);
        }

        public void TitleEnd(SubSection subTitle)
        {
            table.AddRowTitle($"Total {subTitle.Title}", N.F(subTitle.Saldo));
            addEmptyRow = true;
        }

        public void WriteLine(BookkeepingAccount account, decimal betrag)
        {
            table.AddRowEntry($"{account.Konto.KontoNr} {account.Konto.Text}", N.F(betrag));
        }
    }

    public class PdfClosingTable
    {
        private const string STYLE_TABLE = "ClosingTable";
        private const string STYLE_ROW_ENTRY = "ClosingRowEntry";
        private const string STYLE_ROW_TITLE = "ClosingRowTitle";

        public static void DefineStyles(Document document)
        {
            Style style = document.Styles.AddStyle(STYLE_TABLE, StyleNames.Normal);
            style.ParagraphFormat.Alignment = ParagraphAlignment.Left;

            style = document.Styles.AddStyle(STYLE_ROW_ENTRY, STYLE_TABLE);

            style = document.Styles.AddStyle(STYLE_ROW_TITLE, STYLE_ROW_ENTRY);
            style.Font.Bold = true;
        }

        private Table table;
        public PdfClosingTable(PdfDocument document)
        {
            table = document.AddTable();
            table.Style = STYLE_TABLE;
            table.Borders.Color = PdfDocument.TableBorderColor;
            table.Borders.Width = 0.25;
            table.Borders.Left.Width = 0.5;
            table.Borders.Right.Width = 0.5;
            table.Rows.LeftIndent = 0;

            Unit pageWidthWithoutMargins = document.PageWidthWithoutMargins;
            Unit rightIndent = "2.0cm";
            Unit columnWidth1 = "2.0cm";
            Unit columnWidth0 = pageWidthWithoutMargins - rightIndent - columnWidth1;

            table.AddColumn(columnWidth0);
            table.AddColumn(columnWidth1);
        }
        public void AddRowEntry(string left, string right = "")
        {
            AddRow(left, right);
        }
        public void AddRowTitle(string left, string right = "", bool borderVisible = true)
        {
            Row row = AddRow(left, right);
            row.Style = STYLE_ROW_TITLE;
            row.Borders.Visible = borderVisible;
        }
        public void AddRowEmpty()
        {
            AddRow("", "").Borders.Visible = false;
        }

        private Row AddRow(string left, string right)
        {
            Row row = table.AddRow();
            row.Style = STYLE_ROW_ENTRY;
            row.Cells[0].AddParagraph(left);
            row.Cells[1].AddParagraph(right);
            row.Cells[1].Format.Alignment = ParagraphAlignment.Right;
            return row;
        }
    }
    #endregion

    #region Render Account Tables (Kontos)
    public class PdfAccountTable
    {
        private const string STYLE_TABLE = "AccountTable";
        private const string STYLE_ROW_ENTRY = "AccountRowEntry";
        private const string STYLE_ROW_ENTRY_ITALIC = "AccountRowEntryItalic";
        public enum EnumColumn { REFERENCE, TEXT, GEGENKONTO, SOLL, HABEN, SALDO };

        public static void DefineStyles(Document document)
        {
            Style style = document.Styles.AddStyle(STYLE_TABLE, StyleNames.Normal);
            style.ParagraphFormat.Alignment = ParagraphAlignment.Left;

            style = document.Styles.AddStyle(STYLE_ROW_ENTRY, STYLE_TABLE);

            style = document.Styles.AddStyle(STYLE_ROW_ENTRY_ITALIC, STYLE_TABLE);
            style.ParagraphFormat.Font.Italic = true;
            style.Font.Size = 8;
        }

        private Table table;

        public PdfAccountTable(PdfDocument document)
        {
            table = document.AddTable();
            table.Style = STYLE_TABLE;
            table.Borders.Color = PdfDocument.TableBorderColor;
            table.Borders.Width = 0.25;
            table.Borders.Left.Width = 0.5;
            table.Borders.Right.Width = 0.5;
            table.Rows.LeftIndent = 0;

            Unit pageWidthWithoutMargins = document.PageWidthWithoutMargins;
            // COLUMN 0: Reference
            Unit columnWidth0 = "2.5cm";
            // COLUMN 2: G-Konto
            Unit columnWidth2 = "0.9cm";
            // COLUMN 3: Soll
            Unit columnWidth3 = "1.6cm";
            // COLUMN 4: Haben
            Unit columnWidth4 = "1.6cm";
            // COLUMN 5: Saldo
            Unit columnWidth5 = "2.0cm";
            // COLUMN 1: Text
            Unit columnWidth1 = pageWidthWithoutMargins - columnWidth0 - columnWidth2 - columnWidth3 - columnWidth4 - columnWidth5;

            table.AddColumn(columnWidth0);
            table.AddColumn(columnWidth1);
            table.AddColumn(columnWidth2);
            table.AddColumn(columnWidth3);
            table.AddColumn(columnWidth4);
            table.AddColumn(columnWidth5);
        }

        public void AddRowEntry(string reference, string saldo = "", string text = "", string gegenKonto = "", string soll = "", string haben = "")
        {
            string text_ = text;
            if (text_ == null)
            {
                text_ = "";
            }
            TableRow entry = new TableRow(table);
            entry.AddCell(EnumColumn.REFERENCE, reference);
            entry.AddCell(EnumColumn.TEXT, text_);
            entry.AddCell(EnumColumn.GEGENKONTO, gegenKonto, ParagraphAlignment.Center, italic: true);
            entry.AddCell(EnumColumn.SOLL, soll, ParagraphAlignment.Right, italic: true);
            entry.AddCell(EnumColumn.HABEN, haben, ParagraphAlignment.Right, italic: true);
            entry.AddCell(EnumColumn.SALDO, saldo, ParagraphAlignment.Right);
        }

        public class TableRow
        {
            private Row row;
            public TableRow(Table table)
            {
                row = table.AddRow();
                row.Style = STYLE_ROW_ENTRY;
                row.VerticalAlignment = VerticalAlignment.Center;
            }
            public void AddCell(EnumColumn index, string text, ParagraphAlignment alignment = ParagraphAlignment.Left, bool italic = false)
            {
                if (text == "")
                {
                    return;
                }
                Cell cell = row.Cells[(int)index];
                if (italic)
                {
                    cell.Style = STYLE_ROW_ENTRY_ITALIC;
                }
                cell.AddParagraph(text);
                if (alignment != ParagraphAlignment.Left)
                {
                    cell.Format.Alignment = alignment;
                }
            }
        }

        public void AddHeader(string text, decimal saldo = decimal.MinValue)
        {
            // Add Header-Row
            Row row = table.AddRow();
            row.HeadingFormat = true;
            row.Format.Alignment = ParagraphAlignment.Left;
            row.Format.Font.Bold = true;
            row.Borders.Visible = false;

            {
                Cell cell = row.Cells[(int)EnumColumn.REFERENCE];
                cell.AddParagraph(text);
                cell.MergeRight = 2;
            }

            if (saldo == decimal.MinValue)
            {
                {
                    Cell cell = row.Cells[(int)EnumColumn.SOLL];
                    cell.Format.Alignment = ParagraphAlignment.Center;
                    cell.AddParagraph("Soll");
                }

                {
                    Cell cell = row.Cells[(int)EnumColumn.HABEN];
                    cell.Format.Alignment = ParagraphAlignment.Center;
                    cell.AddParagraph("Haben");
                }

                {
                    Cell cell = row.Cells[(int)EnumColumn.SALDO];
                    cell.Format.Alignment = ParagraphAlignment.Right;
                    string saldo_ = saldo == decimal.MinValue ? "" : N.F(saldo);
                    cell.AddParagraph(saldo_);
                }
            }
            else
            {
                {
                    Cell cell = row.Cells[(int)EnumColumn.HABEN];
                    cell.Format.Alignment = ParagraphAlignment.Center;
                    cell.AddParagraph("Total");
                }

                {
                    Cell cell = row.Cells[(int)EnumColumn.SALDO];
                    cell.Format.Alignment = ParagraphAlignment.Right;
                    string saldo_ = saldo == decimal.MinValue ? "" : N.F(saldo);
                    cell.AddParagraph(saldo_);
                }
            }
        }
    }

    public class PdfRendererAccount : IRendererAccount
    {
        private Configuration config;
        private PdfDocument pdf;
        private PdfAccountTable table;
        public BookkeepingAccount Account { get; }

        public PdfRendererAccount(Configuration config_, PdfDocument pdf_, BookkeepingAccount account)
        {
            config = config_;
            pdf = pdf_;
            Account = account;
            Konto konto = Account.Konto;

            pdf.AddH1($"Konto {konto.KontoNr}: {konto.Text}");

            table = new PdfAccountTable(pdf);

            table.AddHeader($"{konto.KontoNr} {konto.Text}");

            table.AddRowEntry(
              reference: config.DateStartValuta,
              saldo: N.F(konto.EroeffnungsKontostand)
            );
        }

        public void WriteEntry(AccountEntry entry, KontoDay day)
        {
            string soll = "";
            string haben = "";
            switch (entry.Relation)
            {
                case BookkeepingRelation.SOLL:
                    soll = N.F(entry.Betrag);
                    break;
                case BookkeepingRelation.HABEN:
                    haben = N.F(entry.Betrag);
                    break;
            }
            //  string buchungstext = entry.Entry.Buchungsvorlage.BuchungsText;
            //  string text = string.IsNullOrEmpty(buchungstext) ? entry.Entry.Kommentar : $"'{buchungstext}' {entry.Entry.Kommentar}";
            string text = entry.Entry.Buchungsvorlage.BuchungsText;
            if (!string.IsNullOrEmpty(text))
            {
                text += ": ";
            }
            text += entry.Entry.Kommentar;
            table.AddRowEntry(
                reference: entry.Referenz,
                // text: $"'{entry.Entry.Buchungsvorlage.VorlageText}' {entry.Entry.Kommentar}",
                // text: entry.Entry.Kommentar,
                text: text,
                gegenKonto: entry.GegenEntry.Konto.KontoNr.ToString(),
                soll: soll,
                haben: haben,
                saldo: day == null ? "" : N.F(day.Saldo)
            );
        }

        public void WriteEndOfAccount(decimal saldo)
        {
            Konto konto = Account.Konto;
            table.AddHeader($"{konto.KontoNr} {konto.Text}", saldo: saldo);
        }
    }

    public class PdfRendererBook : IRendererBook
    {
        private PdfDocument pdf;
        private Configuration config;

        public PdfRendererBook(Configuration config_, PdfDocument pdf_)
        {
            config = config_;
            pdf = pdf_;
        }

        public IRendererAccount CreateAccountRenderer(BookkeepingAccount account)
        {
            return new PdfRendererAccount(config, pdf, account);
        }
    }
    #endregion

}
