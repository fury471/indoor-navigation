using System;
using System.Globalization;
using System.IO;
using IndoorNavigation.Core.Ports;
using UnityEngine;

namespace IndoorNavigation.Adapters.Data
{
    /// <summary>
    /// The real session log: one JSON object per line in
    /// Application.persistentDataPath/SessionLogs, which the Files app and Apple Devices can
    /// reach through the file sharing the app already has.
    /// </summary>
    public sealed class JsonLinesSessionLog : ISessionLog
    {
        private readonly bool echoToConsole;

        public JsonLinesSessionLog(string folder, DateTime startedUtc, bool echoToConsole)
        {
            Directory.CreateDirectory(folder);
            FilePath = Path.Combine(folder, "session-" + startedUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".jsonl");
            this.echoToConsole = echoToConsole;
        }

        public string FilePath { get; }

        public void Append(LogRecord record)
        {
            var line = new LogLineDto
            {
                time = record.TimeUtc.ToString("O", CultureInfo.InvariantCulture),
                participant = record.Participant,
                condition = record.Condition,
                @event = record.EventName,
                detail = record.Detail,
            };

            File.AppendAllText(FilePath, JsonUtility.ToJson(line) + "\n");
            if (echoToConsole)
            {
                Debug.Log("[log] " + record);
            }
        }
    }

    /// <summary>One log line as JSON. "event" is a C# keyword, so the field is written @event.</summary>
    [Serializable]
    internal sealed class LogLineDto
    {
        public string time;
        public string participant;
        public string condition;
        public string @event;
        public string detail;
    }
}
