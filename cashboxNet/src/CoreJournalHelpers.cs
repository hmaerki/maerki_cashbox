using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace cashboxNet
{
    /// <summary>
    /// Mostly empty list: Therefor implementated as lazy list.
    /// </summary>
    public class LazyStringList : IEnumerable<string>
    {
        private List<string> messages = null;
        private static IEnumerable<string> EMPTY { get { yield break; } }
        public bool Empty { get { return messages == null; } }
        public IEnumerable<string> List
        {
            get
            {
                if (messages == null)
                {
                    return EMPTY;
                }
                return messages;
            }
        }
        public void Add(string msg)
        {
            if (messages == null)
            {
                messages = new List<string>();
            }
            messages.Add(msg);
        }

        public IEnumerator<string> GetEnumerator()
        {
            if (messages == null)
            {
                return EMPTY.GetEnumerator();
            }
            return messages.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public interface IDay
    {
        TValuta Valuta { get; }
    }

    public abstract class AbstractDays<T> where T : class, IDay
    {
        private T[] days;
        public T this[TValuta valuta] { get { return days[valuta.DaysAfterDateBegin]; } }
        public IEnumerable<T> DaysOrdered { get { return days.Where<T>(t => t != null); } }

        public AbstractDays(Configuration config)
        {
            Trace.Assert(config.DateStartValuta.DaysAfterDateBegin == 0);
            int totalDays = config.DateEndValuta.DaysAfterDateBegin + 1;
            days = new T[totalDays];
        }

        public bool TryGetDay(TValuta valuta, out T day_)
        {
            try
            {
                T day = days[valuta.DaysAfterDateBegin];
                if (day != null)
                {
                    day_ = day;
                    return true;
                }
                day_ = null;
                return false;
            }
            catch (IndexOutOfRangeException)
            {
                throw new CashboxException($"Das Datum {valuta} ist {-valuta.DaysAfterDateBegin} Tage vor dem Anfang der Buchungsperiode!");
            }

        }

        public T GetDay(TValuta valuta)
        {
            if (TryGetDay(valuta, out T day_))
            {
                return day_;
            }
            T day = CreateDay(valuta);
            days[valuta.DaysAfterDateBegin] = day;
            return day;
        }

        public bool TryGetValue(TValuta valuta, out T day)
        {
            day = this[valuta];
            return day != null;
        }
        protected abstract T CreateDay(TValuta valuta);
    }
}
