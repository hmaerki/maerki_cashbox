
namespace cashboxNet
{
  public class CashboxNetVersion
  {
    public const string Url = "https://github.com/hmaerki/cashbox_git.git"; // "$WCURL$";
    public const string VersionMain = "1.0";
    public const string VersionRevision = "$WCREV$";
    public const string VersionModified = "$WCMODS?Modified:Unmodified$";
    public const string VersionDateTime = "$WCDATE=%Y-%m-%d %H-%M-%S$";
    public const string Version = "v" + VersionMain + "." + VersionRevision + " " + VersionDateTime;
    public const string ProgrammName = "CashboxNet";
    public const string ProgrammNameFull = ProgrammName + " " + Version;
  }
}
