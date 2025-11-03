using System;
using NUnit.Framework;

namespace cashboxNet.Tests
{
    #region Nunit-Tests
    [TestFixture]
    class TestValuta
    {
        [Test]
        public void TestTValuta()
        {
            ValutaFactory f = new ValutaFactory("2017-01-01");
            f.TryParse("2017-02-28", out TValuta valutaOld);
            f.TryParse("2017-03-01", out TValuta valutaMiddle);
            f.TryParse("2017-03-02", out TValuta valutaJoung1);
            f.TryParse("2017-03-02", out TValuta valutaJoung2);

            Assert.That(valutaOld == valutaMiddle, Is.False);
            Assert.That(valutaJoung1 == valutaJoung2, Is.True);

            Assert.That(valutaOld < valutaMiddle, Is.True);
            Assert.That(valutaMiddle < valutaJoung1, Is.True);
            Assert.That(valutaJoung1 < valutaJoung2, Is.False);

            Assert.That(valutaMiddle < valutaOld, Is.False);
            Assert.That(valutaJoung1 < valutaMiddle, Is.False);
            Assert.That(valutaJoung2 < valutaJoung1, Is.False);

            Assert.That(valutaMiddle > valutaOld, Is.True);
            Assert.That(valutaJoung1 > valutaMiddle, Is.True);
            Assert.That(valutaJoung1 > valutaJoung2, Is.False);

            Assert.That(valutaOld > valutaMiddle, Is.False);
            Assert.That(valutaMiddle > valutaJoung1, Is.False);
            Assert.That(valutaJoung2 > valutaJoung1, Is.False);
        }

        [Test]
        public void TestTValutaOperator()
        {
            ValutaFactory f = new ValutaFactory("2017-01-01");
            f.TryParse("2017-02-28", out TValuta valuta1);
            TValuta valuta2 = valuta1 + 1;
            TValuta valuta3 = valuta1 - 1;

            Assert.That(valuta1 < valuta2, Is.True);
            Assert.That(valuta3 < valuta1, Is.True);
        }

        [Test]
        public void TestTValutaFormat()
        {
            ValutaFactory f = new ValutaFactory("2017-01-01");
            f.TryParse("2017-02-28", out TValuta valuta1);

            Assert.That(valuta1.ToString(), Is.EqualTo("2017-02-28"));
        }
    }
    #endregion
}
