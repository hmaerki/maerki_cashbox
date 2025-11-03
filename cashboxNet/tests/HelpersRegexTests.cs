using NUnit.Framework;

namespace cashboxNet.Tests
{
    [TestFixture]
    class ReferenzHelperTest
    {
        [Test]
        [TestCase(0, ExpectedResult = "a")]
        [TestCase(1, ExpectedResult = "b")]
        [TestCase(25, ExpectedResult = "z")]
        [TestCase(26, ExpectedResult = "aa")]
        [TestCase(27, ExpectedResult = "ab")]
        public string TestNumberToAlpha(int number)
        {
            return ReferenzHelper.NumberToAlpha(number);
        }

        [Test]

        [TestCase("a", ExpectedResult = 0)]
        [TestCase("b", ExpectedResult = 1)]
        [TestCase("z", ExpectedResult = 25)]
        [TestCase("aa", ExpectedResult = 26)]
        [TestCase("ab", ExpectedResult = 27)]
        public int TestAlphaToNumber(string alpha)
        {
            return ReferenzHelper.AlphaToNumber(alpha);
        }
    }
}
