using System;
using System.Collections.Generic;
using Mond.Binding;
using Mond.Libraries;

namespace MondHost.Libraries
{
    [MondClass("DateTime")]
    class DateTimeClass
    {
        private DateTimeOffset _value;

        public DateTimeClass(DateTimeOffset value) => _value = value;

        [MondFunction]
        public int Year => _value.Year;

        [MondFunction]
        public int Month => _value.Month;

        [MondFunction]
        public int Day => _value.Day;

        [MondFunction]
        public int Hour => _value.Hour;

        [MondFunction]
        public int Minute => _value.Minute;

        [MondFunction]
        public int Second => _value.Second;

        [MondFunction]
        public int Millisecond => _value.Millisecond;

        [MondFunction]
        public int Offset => (int)_value.Offset.TotalSeconds;

        [MondFunction]
        public string DayOfWeek => _value.DayOfWeek.ToString();

        [MondFunction]
        public int DayOfYear => _value.DayOfYear;

        [MondFunction("addYears")]
        public DateTimeClass AddYears(int years) => new DateTimeClass(_value.AddYears(years));

        [MondFunction("addMonths")]
        public DateTimeClass AddMonths(int months) => new DateTimeClass(_value.AddMonths(months));

        [MondFunction("addDays")]
        public DateTimeClass AddDays(int days) => new DateTimeClass(_value.AddDays(days));

        [MondFunction("addHours")]
        public DateTimeClass AddHours(int hours) => new DateTimeClass(_value.AddHours(hours));

        [MondFunction("addMinutes")]
        public DateTimeClass AddMinutes(int minutes) => new DateTimeClass(_value.AddMinutes(minutes));

        [MondFunction("addSeconds")]
        public DateTimeClass AddSeconds(int seconds) => new DateTimeClass(_value.AddSeconds(seconds));

        [MondFunction("addMilliseconds")]
        public DateTimeClass AddMilliseconds(int milliseconds) => new DateTimeClass(_value.AddYears(milliseconds));

        [MondFunction("toLocalTime")]
        public DateTimeClass ToLocalTime() => new DateTimeClass(_value.ToLocalTime());

        [MondFunction("toUniversalTime")]
        public DateTimeClass ToUniversalTime() => new DateTimeClass(_value.ToUniversalTime());

        [MondFunction("toUnixTimeSeconds")]
        public double ToUnixTimeSeconds() => _value.ToUnixTimeSeconds();

        [MondFunction("toUnixTimeMilliseconds")]
        public double ToUnixTimeMilliseconds() => _value.ToUnixTimeMilliseconds();

        [MondFunction("toString")]
        public override string ToString() => _value.ToString();

        [MondFunction("toString")]
        public string ToString(string format) => _value.ToString(format);

        [MondFunction("__string")]
        public string CastToString(DateTimeClass _) => ToString();

        [MondFunction("__eq")]
        public bool Equals(DateTimeClass x, DateTimeClass y) => x._value == y._value;

        [MondFunction("__eq")]
        public bool Equals(MondValue x, MondValue y) => false;

        [MondFunction("__gt")]
        public bool GreaterThan(DateTimeClass x, DateTimeClass y) => x._value > y._value;
    }

    [MondModule("DateTime")]
    static class DateTimeModule
    {
        [MondFunction("__call")]
        public static DateTimeClass New(MondValue _,
            int year, int month = 1, int day = 1,
            int hour = 0, int minute = 0, int second = 0, int millisecond = 0,
            int offsetSeconds = 0)
        {
            var offsetSpan = TimeSpan.FromSeconds(offsetSeconds);
            var dto = new DateTimeOffset(year, month, day, hour, minute, second, millisecond, offsetSpan);
            return new DateTimeClass(dto);
        }

        [MondFunction("now")]
        public static DateTimeClass Now() => new DateTimeClass(DateTimeOffset.Now);

        [MondFunction("utcNow")]
        public static DateTimeClass UtcNow() => new DateTimeClass(DateTimeOffset.UtcNow);

        [MondFunction("fromUnixTimeSeconds")]
        public static DateTimeClass FromUnixTimeSeconds(double seconds) =>
            new DateTimeClass(DateTimeOffset.FromUnixTimeSeconds((long)seconds));

        [MondFunction("fromUnixTimeSeconds")]
        public static DateTimeClass FromUnixTimeSeconds(string seconds) =>
            new DateTimeClass(DateTimeOffset.FromUnixTimeSeconds(long.Parse(seconds)));

        [MondFunction("fromUnixTimeMilliseconds")]
        public static DateTimeClass FromUnixTimeMilliseconds(double milliseconds) =>
            new DateTimeClass(DateTimeOffset.FromUnixTimeMilliseconds((long)milliseconds));

        [MondFunction("fromUnixTimeMilliseconds")]
        public static DateTimeClass FromUnixTimeMilliseconds(string milliseconds) =>
            new DateTimeClass(DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(milliseconds)));

        [MondFunction("parse")]
        public static DateTimeClass Parse(string value) => new DateTimeClass(DateTimeOffset.Parse(value));
    }

    class DateTimeLibraries : IMondLibraryCollection
    {
        public IEnumerable<IMondLibrary> Create(MondState state)
        {
            yield return new DateTimeLibrary(state);
        }
    }

    class DateTimeLibrary : IMondLibrary
    {
        private readonly MondState _state;

        public DateTimeLibrary(MondState state) => _state = state;

        public IEnumerable<KeyValuePair<string, MondValue>> GetDefinitions()
        {
            var module = MondModuleBinder.Bind(typeof(DateTimeModule), _state);
            MondClassBinder.Bind<DateTimeClass>(_state);
            yield return new KeyValuePair<string, MondValue>("DateTime", module);
        }
    }
}
