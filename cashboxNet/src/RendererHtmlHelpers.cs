using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Web;

namespace cashboxNet
{

  /// <summary>
  /// Represents a Html-Tag.
  /// Used to format a Html-Page.
  /// </summary>
  public class HtmlTag
  {
    private List<HtmlTag> children = null;
    private string tag;
    private string args = "";
    private string text = null;
    private bool parentAllowIndent = true;
    private bool allowIndent = true;
    public HtmlTag(string tag_, string args_ = null, string text_ = null, string textRaw_ = null)
    {
      tag = tag_;
      if (tag == "td")
      {
        allowIndent = false;
      }
      if (args_ != null)
      {
        Trace.Assert(tag_ != null);
        args = " " + args_.Replace('\'', '"');
      }
      if (text_ != null)
      {
        Trace.Assert(textRaw_ == null);
        text = HttpUtility.HtmlEncode(text_);
      }
      if (textRaw_ != null)
      {
        Trace.Assert(text_ == null);
        text = textRaw_;
      }
    }
    public HtmlTag Append(string tag_ = null, string args = null, string text = null, string textRaw = null)
    {
      HtmlTag tag = new HtmlTag(tag_, args, text, textRaw);
      Append(tag);
      return tag;
    }
    private void Append(HtmlTag tag)
    {
      tag.parentAllowIndent = allowIndent;
      if (!allowIndent)
      {
        // If our parent ist '<td>', we also have to avoid intents.
        tag.allowIndent = false;
      }
      if (children == null)
      {
        children = new List<HtmlTag>();
      }
      children.Add(tag);
    }
    public void Write(HtmlStreamWriter html, string indent)
    {
      if (tag == null)
      {
        // No args, sub children!
        Trace.Assert(string.IsNullOrEmpty(args));
        Trace.Assert(children == null);
        Trace.Assert(text != null);
        html.WriteRaw(text);
        return;
      }

      if (children == null)
      {
        if (text == null)
        {
          html.WriteLineRaw($"<{tag}{args}/>", indent: parentAllowIndent ? indent : null, lineFeed: allowIndent);
          return;
        }
        html.WriteLineRaw($"<{tag}{args}>{text}</{tag}>", indent: parentAllowIndent ? indent : null, lineFeed: allowIndent);
        return;
      }

      Trace.Assert(text == null);
      html.WriteLineRaw($"<{tag}{args}>", indent: parentAllowIndent ? indent : null, lineFeed: allowIndent);
      foreach (HtmlTag tag in children)
      {
        tag.Write(html, indent: indent + "  ");
      }
      html.WriteLineRaw($"</{tag}>", indent: allowIndent ? indent : null, lineFeed: parentAllowIndent);
    }
  }

  /// <summary>
  /// Encapsulates a HTML-File.
  /// Writes the Header including Styles.
  /// </summary>
  public class HtmlStreamWriter : IDisposable
  {
    private StreamWriter fs;
    private Configuration config;
    public HtmlTag Body { get; private set; }

    public HtmlStreamWriter(Configuration config_, string filename)
    {
      fs = new StreamWriter(filename);
      config = config_;
      WriteHeader();
      Body = new HtmlTag("body");

      Body.Append("a", args: $"name='{EnumAnchors.TOP}'");
      Body.Append("h1", text: config.Title);
      Body.Append("h2", text: "Index");
      Body.Append("p", text: $"Periode: {config.DatePeriod}");
      Body.Append("p", text: $"Reporterstellung: {config.NowTimeStamp}");
      Body.Append("p", text: $"{MuhFile.FILENAME}: {config.MuhFileTimeStamp}");
      Body.Append("p", text: CashboxNetVersion.ProgrammNameFull);
    }

    public void Dispose()
    {
      Body.Write(this, indent: "");
      WriteLineRaw("</html>");
      ((IDisposable)fs).Dispose();
    }

    private void WriteHeader()
    {
      string resource = "cashboxNet.resources.RendererHtmlHeader.html";
      using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
      if (stream == null)
      {
        throw new InvalidOperationException($"Resouces '{resource}' not found!");
      }
      using var reader = new StreamReader(stream);
      string header = reader.ReadToEnd();
      header = header.Replace("<<strTitleShort>>", config.TitleShort);

      fs.WriteLine(header);
    }

    public void WriteRaw(string html)
    {
      fs.Write(html);
    }

    public void WriteLineRaw(string html, string indent = null, bool lineFeed = true)
    {
      if (indent != null)
      {
        fs.Write(indent);
      }
      fs.Write(html);
      if (lineFeed)
      {
        fs.WriteLine();
      }
    }
  }
}
