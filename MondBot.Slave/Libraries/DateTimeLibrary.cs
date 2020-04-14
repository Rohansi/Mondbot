using System.Collections.Generic;
using Mond;
using Mond.Binding;
using Mond.Libraries;
using NodaTime;
using NodaTime.Text;
using NodaTime.TimeZones;

namespace MondBot.Slave.Libraries
{
    [MondClass("DateTime")]
    class DateTimeClass
    {
        private ZonedDateTime _value;

        public DateTimeClass(ZonedDateTime value) => _value = value;

        public DateTimeClass(Instant instant, string timeZone = null) =>
            _value = new ZonedDateTime(instant, DateTimeHelper.Find(timeZone));

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
        public int Offset => _value.Offset.Seconds;

        [MondFunction]
        public string DayOfWeek => _value.DayOfWeek.ToString();

        [MondFunction]
        public int DayOfYear => _value.DayOfYear;

        [MondFunction("isDaylightSavingsTime")]
        public bool IsDaylightSavingsTime() => _value.IsDaylightSavingTime();

        /*[MondFunction("addYears")]
        public DateTimeClass AddYears(int years) => new DateTimeClass(_value.AddYears(years));

        [MondFunction("addMonths")]
        public DateTimeClass AddMonths(int months) => new DateTimeClass(_value.AddMonths(months));

        [MondFunction("addDays")]
        public DateTimeClass AddDays(int days) => new DateTimeClass(_value.AddDays(days));*/

        [MondFunction("addHours")]
        public DateTimeClass AddHours(int hours) => new DateTimeClass(_value.PlusHours(hours));

        [MondFunction("addMinutes")]
        public DateTimeClass AddMinutes(int minutes) => new DateTimeClass(_value.PlusMinutes(minutes));

        [MondFunction("addSeconds")]
        public DateTimeClass AddSeconds(int seconds) => new DateTimeClass(_value.PlusSeconds(seconds));

        [MondFunction("addMilliseconds")]
        public DateTimeClass AddMilliseconds(int milliseconds) => new DateTimeClass(_value.PlusMilliseconds(milliseconds));

        [MondFunction("toTimeZone")]
        public DateTimeClass ToTimeZone(string id) =>
            new DateTimeClass(_value.WithZone(TzdbDateTimeZoneSource.Default.ForId(id)));

        [MondFunction("toUniversalTime")]
        public DateTimeClass ToUniversalTime() => new DateTimeClass(_value.WithZone(DateTimeZone.Utc));

        [MondFunction("toUnixTimeSeconds")]
        public double ToUnixTimeSeconds() => _value.ToInstant().ToUnixTimeSeconds();

        [MondFunction("toUnixTimeMilliseconds")]
        public double ToUnixTimeMilliseconds() => _value.ToInstant().ToUnixTimeMilliseconds();

        [MondFunction("toString")]
        public override string ToString() => _value.ToString();

        [MondFunction("toString")]
        public string ToString(string format) => _value.ToString(format, null);

        [MondFunction("__string")]
        public string CastToString(DateTimeClass _) => ToString();

        [MondFunction("__eq")]
        public bool Equals(DateTimeClass x, DateTimeClass y) => x._value == y._value;

        [MondFunction("__eq")]
        public bool Equals(MondValue x, MondValue y) => false;

        [MondFunction("__gt")]
        public bool GreaterThan(DateTimeClass x, DateTimeClass y) => x._value.ToInstant() > y._value.ToInstant();

        [MondFunction("__serialize")]
        public MondValue Serialize(MondState state, params MondValue[] args)
        {
            var result = MondValue.Object(state);
            result["$ctor"] = "DateTime";
            result["$args"] = MondValue.Array(new MondValue[] { _value.ToInstant().ToUnixTimeSeconds(), _value.Zone.Id });
            return result;
        }
    }

    [MondModule("DateTime")]
    static class DateTimeModule
    {
        [MondFunction("__call")]
        public static DateTimeClass New(MondValue _,
            int year, int month = 1, int day = 1,
            int hour = 0, int minute = 0, int second = 0, int millisecond = 0,
            string timeZone = null)
        {
            var local = new LocalDateTime(year, month, day, hour, minute, second, millisecond);
            return new DateTimeClass(local.InZoneLeniently(DateTimeHelper.Find(timeZone)));
        }

        [MondFunction("__call")]
        public static DateTimeClass New(MondValue _, double unixSeconds, string timeZone = null)
        {
            var instant = Instant.FromUnixTimeSeconds((long)unixSeconds);
            return new DateTimeClass(instant, timeZone);
        }

        [MondFunction("now")]
        public static DateTimeClass Now(string timeZone = null) =>
            new DateTimeClass(SystemClock.Instance.GetCurrentInstant(), timeZone);

        [MondFunction("fromUnixTimeSeconds")]
        public static DateTimeClass FromUnixTimeSeconds(double seconds) =>
            new DateTimeClass(Instant.FromUnixTimeSeconds((long)seconds));

        [MondFunction("fromUnixTimeSeconds")]
        public static DateTimeClass FromUnixTimeSeconds(string seconds) =>
            new DateTimeClass(Instant.FromUnixTimeSeconds(long.Parse(seconds)));

        [MondFunction("fromUnixTimeMilliseconds")]
        public static DateTimeClass FromUnixTimeMilliseconds(double milliseconds) =>
            new DateTimeClass(Instant.FromUnixTimeMilliseconds((long)milliseconds));

        [MondFunction("fromUnixTimeMilliseconds")]
        public static DateTimeClass FromUnixTimeMilliseconds(string milliseconds) =>
            new DateTimeClass(Instant.FromUnixTimeMilliseconds(long.Parse(milliseconds)));

        [MondFunction("parse")]
        public static DateTimeClass Parse(string text, string patternText = null)
        {
            patternText = patternText ?? ZonedDateTimePattern.GeneralFormatOnlyIso.PatternText;
            var pattern = ZonedDateTimePattern.CreateWithInvariantCulture(patternText, DateTimeHelper.Cache);

            if (!pattern.Parse(text).TryGetValue(new ZonedDateTime(), out var result))
                throw new MondRuntimeException("DateTime.arse: Failed to parse input text");

            return new DateTimeClass(result);
        }
    }

    static class DateTimeHelper
    {
        public static DateTimeZoneCache Cache { get; }

        static DateTimeHelper()
        {
            Cache = new DateTimeZoneCache(TzdbDateTimeZoneSource.Default);
        }

        public static DateTimeZone Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return DateTimeZone.Utc;

            var timeZone = Cache.GetZoneOrNull(id);
            if (timeZone == null)
                throw new MondRuntimeException($"Timezone '{id}' was not found");

            return timeZone;
        }
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
