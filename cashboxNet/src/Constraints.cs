namespace cashboxNet
{
  public interface IConstraint
  {
    void UpdateByConfig(Configuration config);
    void ApplyBookkeepingConstraints(Configuration config, Journal journal, BookkeepingBook book);
  }

  public class ConstraintZeroSaldoEveryDay : IConstraint
  {
    public Konto Konto { get; private set; }
    private readonly int kontoNr;

    public ConstraintZeroSaldoEveryDay(int kontoNr_)
    {
      kontoNr = kontoNr_;
    }

    public void UpdateByConfig(Configuration config)
    {
      Konto = config.FindKonto(kontoNr);
    }

    public void ApplyBookkeepingConstraints(Configuration config, Journal journal, BookkeepingBook book)
    {
      BookkeepingAccount account = book[Konto];
      foreach (KontoDay kontoDay in account.Konto.KontoDays.DaysOrdered)
      {
        if (kontoDay.Saldo != 0)
        {
          kontoDay.MessagesErrors.Add($"Der Saldo von Konto {kontoNr} ist {N.F(kontoDay.Saldo)}, sollte aber 0.00 sein! (ConstraintZeroSaldoEveryDay)");
        }
      }
    }

  }

  public class ConstraintZeroSaldoEndOfYear : IConstraint
  {
    public Konto Konto { get; private set; }
    private readonly int kontoNr;

    public ConstraintZeroSaldoEndOfYear(int kontoNr_)
    {
      kontoNr = kontoNr_;
    }

    public void UpdateByConfig(Configuration config)
    {
      Konto = config.FindKonto(kontoNr);
    }

    public void ApplyBookkeepingConstraints(Configuration config, Journal journal, BookkeepingBook book)
    {
      if (journal.GewinnBuchungEntry == null)
      {
        return;
      }

      BookkeepingAccount account = book[Konto];
      TValuta Valuta = journal.GewinnBuchungEntry.Valuta;
      decimal saldo = account.GetSaldo(Valuta);
      if (saldo != 0M)
      {
        journal.GewinnBuchungEntry.MessagesErrors.Add($"Der Gewinn wurde verbucht. Der Saldo von Konto {kontoNr} '{Konto.Text}' ist {N.F(saldo)}, sollte aber 0.00 sein! (ConstraintEndOfYearZeroSaldo)");
      }
    }
  }

  public class KlangspielZahlungseingangConstraint : IConstraint
  {
    public Konto Konto { get; private set; }
    public string Directory { get; private set; }
    private readonly int kontoNr;

    public KlangspielZahlungseingangConstraint(int kontoNr_, string directory)
    {
      kontoNr = kontoNr_;
      Directory = directory;
    }

    public void UpdateByConfig(Configuration config)
    {
      Konto = config.FindKonto(kontoNr);
    }

    public void ApplyBookkeepingConstraints(Configuration config, Journal journal, BookkeepingBook book)
    {
      KlangspielAutomation klangspielAutomation = new KlangspielAutomation(this, config, book, journal);
      klangspielAutomation.Run();
    }
  }
}
