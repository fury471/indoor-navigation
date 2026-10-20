using System;
using System.Globalization;

namespace IndoorNavigation.Core.Ports
{
    /// <summary>One time-stamped line of the test log.</summary>
    public sealed class LogRecord
    {
        public LogRecord(DateTime timeUtc, string participant, string condition, string eventName, string detail)
        {
            TimeUtc = timeUtc;
            Participant = participant;
            Condition = condition;
            EventName = eventName;
            Detail = detail;
        }

        public DateTime TimeUtc { get; }
        public string Participant { get; }
        public string Condition { get; }
        public string EventName { get; }
        public string Detail { get; }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0:O} {1} {2} {3}: {4}", TimeUtc, Participant, Condition, EventName, Detail);
    }

    /// <summary>
    /// Somewhere to write the test log. The core decides what is logged; the adapter decides
    /// how it is stored. The real version is used from day one.
    /// </summary>
    public interface ISessionLog
    {
        void Append(LogRecord record);
    }
}
