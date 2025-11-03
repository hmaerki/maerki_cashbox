using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace cashboxNet
{
    public class ValutaFactory
    {
        private DateTime dateBegin = new DateTime();
        private static string DATETIME_FORMAT = "yyyy-MM-dd";
        private static string[] DATETIME_FORMATS = { DATETIME_FORMAT, };
        public TValuta UNDEFINED;

        #region Singleton
        private static ValutaFactory singleton = null;

        public static ValutaFactory SingletonInit(DateTime dateBegin)
        {
            if (singleton != null)
            {
                Assert(dateBegin == singleton.dateBegin, "Singleton is already initialized, but with another startdate!");
            }
            else
            {
                singleton = new ValutaFactory(dateBegin);
            }
            return singleton;
        }

        public static ValutaFactory SingletonInit(string s)
        {
            DateTime dateBegin = ParseExact(s);
            SingletonInit(dateBegin);
            return singleton;
        }

        public static ValutaFactory Singleton()
        {
            Assert(singleton != null, "Singleton was not initialized");
            return singleton;
        }

        public static void SingletonReset()
        {
            singleton = null;
        }
        #endregion

        #region constructor
        public ValutaFactory(DateTime dateBegin_)
        {
            dateBegin = dateBegin_;
            UNDEFINED = new TValuta(this, int.MinValue);
        }

        private static DateTime ParseExact(string s)
        {
            return DateTime.ParseExact(s, DATETIME_FORMAT, CultureInfo.InstalledUICulture, DateTimeStyles.NoCurrentDateDefault);
        }

        /// <summary>
        /// 2017-01-01
        /// </summary>
        /// <param name="s"></param>
        public ValutaFactory(string s)
        {
            dateBegin = ParseExact(s);
        }
        #endregion

        // [Conditional("DEBUG")]
        public static void Assert(bool success, string msg)
        {
            if (!success)
            {
                throw new ArgumentException($"class ValutaFactory: {msg}");
            }
        }

        public bool TryParse(string dateString, out TValuta valuta)
        {
            if (DateTime.TryParseExact(dateString, DATETIME_FORMATS, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
            {
                int daysAfterDateBegin = CalculateDaysAfterBegin(date);
                valuta = new TValuta(this, daysAfterDateBegin);
                return true;
            }
            valuta = UNDEFINED;
            return false;
        }

        public string Format(int daysAfterDateBegin)
        {
            DateTime dateTime = dateBegin.AddDays(daysAfterDateBegin);
            return dateTime.ToString(DATETIME_FORMAT);
        }

        public int CalculateDaysAfterBegin(DateTime date)
        {
            return (date - dateBegin).Days;
        }

        public int CalculateDaysAfterBegin(string s)
        {
            DateTime date = ParseExact(s);
            return CalculateDaysAfterBegin(date);
        }
    }

    public struct TValuta : IComparable, IComparable<TValuta>, IEquatable<TValuta>
    {
        public int DaysAfterDateBegin { get; private set; }
        public bool IsDefined { get { return this != factory.UNDEFINED; } }
        private ValutaFactory factory;
        public TValuta(ValutaFactory factory_, int daysAfterDateBegin_)
        {
            factory = factory_;
            DaysAfterDateBegin = daysAfterDateBegin_;
        }
        public TValuta(int daysAfterDateBegin_)
        {
            factory = ValutaFactory.Singleton();
            DaysAfterDateBegin = daysAfterDateBegin_;
        }
        public TValuta(DateTime date)
        {
            factory = ValutaFactory.Singleton();
            DaysAfterDateBegin = factory.CalculateDaysAfterBegin(date);
        }
        public TValuta(string date)
        {
            factory = ValutaFactory.Singleton();
            DaysAfterDateBegin = factory.CalculateDaysAfterBegin(date);
        }
        public override string ToString()
        {
            return factory.Format(DaysAfterDateBegin);
        }
        // User-defined conversion from TValuta to string
        public static implicit operator string(TValuta valuta)
        {
            return valuta.ToString();
        }
        public TValuta UNDEFINED { get { return factory.UNDEFINED; } }

        public int CompareTo(object obj)
        {
            return CompareTo((TValuta)obj);
        }

        /// <summary>
        /// Returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
        /// </summary>
        public int CompareTo(TValuta other)
        {
            ValutaFactory.Assert(factory == other.factory, $"{typeof(TValuta).Name}: Different factories used!");
            return DaysAfterDateBegin.CompareTo(other.DaysAfterDateBegin);
        }

        public override bool Equals(object other_)
        {
            if (other_ is TValuta)
            {
                return Equals((TValuta)other_);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return DaysAfterDateBegin.GetHashCode();
        }
        public bool Equals(TValuta other)
        {
            ValutaFactory.Assert(factory == other.factory, $"{typeof(TValuta).Name}: Different factories used!");
            return DaysAfterDateBegin == other.DaysAfterDateBegin;
        }

        public static bool operator ==(TValuta left, TValuta right)
        {
            ValutaFactory.Assert(left.factory == right.factory, $"{typeof(TValuta).Name}: Different factories used!");
            return left.DaysAfterDateBegin == right.DaysAfterDateBegin;
        }

        public static bool operator !=(TValuta left, TValuta right)
        {
            ValutaFactory.Assert(left.factory == right.factory, $"{typeof(TValuta).Name}: Different factories used!");
            return left.DaysAfterDateBegin != right.DaysAfterDateBegin;
        }

        public static bool operator >(TValuta left, TValuta right)
        {
            ValutaFactory.Assert(left.factory == right.factory, $"{typeof(TValuta).Name}: Different factories used!");
            return left.DaysAfterDateBegin > right.DaysAfterDateBegin;
        }

        public static bool operator <(TValuta left, TValuta right)
        {
            ValutaFactory.Assert(left.factory == right.factory, $"{typeof(TValuta).Name}: Different factories used!");
            return left.DaysAfterDateBegin < right.DaysAfterDateBegin;
        }

        public static bool operator >=(TValuta left, TValuta right)
        {
            ValutaFactory.Assert(left.factory == right.factory, $"{typeof(TValuta).Name}: Different factories used!");
            return left.DaysAfterDateBegin >= right.DaysAfterDateBegin;
        }

        public static bool operator <=(TValuta left, TValuta right)
        {
            ValutaFactory.Assert(left.factory == right.factory, $"{typeof(TValuta).Name}: Different factories used!");
            return left.DaysAfterDateBegin <= right.DaysAfterDateBegin;
        }

        /// <summary></summary>
        /// <returns>Difference in days</returns>
        public static int operator -(TValuta left, TValuta right)
        {
            ValutaFactory.Assert(left.factory == right.factory, $"{typeof(TValuta).Name}: Different factories used!");
            return left.DaysAfterDateBegin - right.DaysAfterDateBegin;
        }

        /// <summary></summary>
        /// <returns>Add/remove a number of days</returns>
        public static TValuta operator +(TValuta left, int right)
        {
            return new TValuta(left.factory, left.DaysAfterDateBegin + right);
        }

        /// <summary></summary>
        /// <returns>Add/remove a number of days</returns>
        public static TValuta operator -(TValuta left, int right)
        {
            return new TValuta(left.factory, left.DaysAfterDateBegin - right);
        }
    }

}
