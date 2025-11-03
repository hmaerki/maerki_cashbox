using System;
using System.IO;

namespace cashboxNet
{
  public class BankRaiffeisen : IBankFactory
  {
    public string Filename { get; private set; }
    public string Name { get; private set; }
    public Konto KontoBank { get; private set; }
    private readonly int kontoNr;
    public bool KontostandUeberpruefen { get; private set; }
    public bool AddBuchungsvorschlaege { get { return KontostandUeberpruefen; } }

    public BankRaiffeisen(int kontoNr_, string name, string filename = "journal_raiffeisen.mt940", bool kontostandUeberpruefen = true)
    {
      kontoNr = kontoNr_;
      Name = name;
      Filename = filename;
      KontostandUeberpruefen = kontostandUeberpruefen;
    }

    public IBankReader Factory(Configuration config, string directory)
    {
      string extension = Path.GetExtension(Filename).ToLower();
      if (extension == ".xml")
      {
        return new BankReaderISO20200(config, directory, this);
      }
      return new BankReaderMT940(config, directory, this);
    }

    public void UpdateByConfig(Configuration config)
    {
      KontoBank = config.FindKonto(kontoNr);
    }
  }
}
