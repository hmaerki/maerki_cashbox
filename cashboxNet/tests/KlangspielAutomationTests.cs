using System;
using NUnit.Framework;

namespace cashboxNet.Tests
{
    [TestFixture]
    public class RegexpKlangspielRechnungTest
    {
        [TestCase(true, true, "2017-01-03 168.50 klangspielschweiz 2017-01-03_09-05-10_vesr22244_vorauskasse_R22244.kagi-mller.doc", ExpectedResult = true)]
        [TestCase(false, true, "2017-01-03 168.50 klangspielschweiz 2017-01-03_09-05-10_vorauskasse_R22244.kagi-mller.doc", ExpectedResult = true)]
        [TestCase(true, false, "2017-01-03 168.50 klangspielschweiz 2017-01-03_09-05-10_vesr22244_R22244.kagi-mller.doc", ExpectedResult = true)]
        [TestCase(false, false, "2017-01-03 168.50 klangspielschweiz 2017-01-03_09-05-10_R22244.kagi-mller.doc", ExpectedResult = true)]
        [TestCase(true, false, KlangspielAutomation.SAMPLE, ExpectedResult = true)]
        [TestCase(true, false, "2017-01-03 168.50 klangspielschweiz 2017-01-03_09-05-10_vesr22244", ExpectedResult = true)]
        [TestCase(true, false, "2017-01-03 168.50 klangspielschweiz 2017-01-03_09-05-10_vesr22244_", ExpectedResult = true)]
        public bool TestRegexpKlangspielRechnung(bool vesr, bool vorauskasse, string fileDirectoryName)
        {
            ValutaFactory.SingletonReset();
            ValutaFactory.SingletonInit("2017-01-01");

            RegexpKlangspielRechnung t = RegexpKlangspielRechnung.TryMatch(fileDirectoryName);
            if (t != null)
            {
                Assert.That(t.Betrag, Is.EqualTo(168.50M));
                Assert.That(t.Vorlagebuchung, Is.EqualTo("klangspielschweiz"));
                Assert.That(t.Date.ToString(), Is.EqualTo("2017-01-03"));
                Assert.That(t.Vorauskasse, Is.EqualTo(vorauskasse));
                if (vesr)
                {
                    Assert.That(t.VESR, Is.EqualTo("22244"));
                }
                return true;
            }
            return false;
        }
    }
}
