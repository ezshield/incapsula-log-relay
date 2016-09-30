using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace EZShield.Incapsula.LogRelay
{
    public class AppState
    {
        public DateTime StartupTime
        { get; set; }

        public DateTime LastCycleTime
        { get; set; }

        public string LastCycleError
        { get; set; }

        public SortedDictionary<string, LogFileState> LogFiles
        { get; } = new SortedDictionary<string, LogFileState>();

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public void Save(string path)
        {
            var s = ToJson();
            File.WriteAllText(path, s);
        }
        public static AppState Parse(string s)
        {
            return JsonConvert.DeserializeObject<AppState>(s);
        }

        public static AppState Load(string path)
        {
            var s = System.IO.File.ReadAllText(path);
            return Parse(s);
        }

    }

    public class LogFileState
    {
        public DateTime? CreateTime
        { get; set; }

        public int PullTryCount
        { get; set; }

        public DateTime? PullTryTime
        { get; set; }

        public string PullError
        { get; set; }

        public DateTime? PullTime
        { get; set; }

        public DateTime? PushTryTime
        { get; set; }

        public int PushTryCount
        { get; set; }

        public string PushError
        { get; set; }

        public DateTime? PushTime
        { get; set; }
    }
}