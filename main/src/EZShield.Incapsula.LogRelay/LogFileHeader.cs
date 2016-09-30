using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace EZShield.Incapsula.LogRelay
{
    /// <summary>
    /// This classes parses and captures the header
    /// section of an individual Incapsula log file.
    /// </summary>
    /// <remarks>
    /// In addition to the pre-defined and well-known fields, if any
    /// unknown or unexpected fields are encountered during parsing
    /// they are captured and made available as "named values" that
    /// can be iterated.
    /// </remarks>
    [JsonObject] // Annotated to prevent default serialization as a list
    public class LogFileHeader : IEnumerable<NamedValue>
    {
        private static readonly NLog.Logger LOG = NLog.LogManager.GetCurrentClassLogger();

        public static readonly TypeInfo THIS_TYPE_INFO = typeof(LogFileHeader).GetTypeInfo();

        public static readonly Regex LINES_SPLITTER_REGEX = new Regex("\\r?\\n");
        
        public static readonly Regex KEY_VALUE_SPLITTER_REGEX = new Regex(":");

        private OrderedDictionary _unknown = null;

        /// Get or set an unknown/unexpected header value. 
        public string this[string key]
        {
            get
            {
                return _unknown?[key] as string;
            }
            set
            {
                if (_unknown == null)
                    _unknown = new OrderedDictionary();
                _unknown[key] = value;
            }
        }

        /// startTime: The start time of the current log file.
        /// <example>startTime:1474886387382</example>
        public long StartTime
        { get; set; }
        
        /// endTime: The end time of the current log file.
        /// <example>endTime:1474886672511</example>
        public long EndTime
        { get; set; }

        /// accountID: Your Account ID.
        /// <example>accountId:405057</example>
        public string AccountId
        { get; set; }

        /// format: The format of the events in the log file: CEF , LEEF or W3C (example).
        /// <example>format:CEF</example>
        public string Format
        { get; set; }

        /// checksum: An MD5 checksum that verifies that the entire file content has not been tampered with.
        /// <example>checksum:f74fa21ddc3059e802507a5d48875d58</example>
        public string Checksum
        { get; set; }

        /// publicKeyId: Public Key ID.
        /// <example></example>
        public string PublicKeyId
        { get; set; }

        /// key: The log content decryption key.
        /// <example></example>
        public string Key
        { get; set; }

        /// configID: The configuration ID. Each account has a configuration ID.
        /// <example>configId:618</example>
        public string ConfigId
        { get; set; }

        /// W3C fields: For W3C format, it is required to present the list of fields.
        /// <example></example>
        public string W3C_Fields
        { get; set; }

        /// Get a count of unknown/unexpected header values. 
        public int GetUnknownCount()
        {
            return (_unknown?.Count).GetValueOrDefault();
        }

        /// Enumerate through all unknown/unexpected header name/values.        
        public IEnumerator<NamedValue> GetUnknown()
        {
            if (_unknown == null)
                yield break;
            
            foreach (var x in _unknown)
            {
                var de = (DictionaryEntry)x;
                yield return new NamedValue(
                        (string)de.Key,
                        (string)de.Value);
            }
        }

        /// Returns all unknown/unexpected header name/values.
        IEnumerator<NamedValue> IEnumerable<NamedValue>.GetEnumerator()
        {
            return GetUnknown();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetUnknown();
        }

        public static LogFileHeader Parse(string rawHeader)
        {
            var header = new LogFileHeader();

            var lines = LINES_SPLITTER_REGEX.Split(rawHeader.Trim());
            foreach (var l in lines)
            {
                var kv = KEY_VALUE_SPLITTER_REGEX.Split(l, 2);
                var key = kv[0];
                var val = kv.Length > 1 ? kv[1] : null;

                key = key.Replace(' ', '_'); // Some of the keys may have spaces                
                var prop = THIS_TYPE_INFO.GetProperty(key, BindingFlags.Public
                        | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (prop != null)
                {
                    // Bind known keys to the value using strong-typing
                    if (prop.PropertyType.IsInstanceOfType(val))
                        prop.SetValue(header, val);
                    else
                    {
                        prop.SetValue(header, TypeDescriptor.GetConverter(
                                prop.PropertyType).ConvertFromString(val)); 
                    }
                }
                else
                {
                    // All unknown/undefined property keys
                    // get thrown into key/value store
                    header[kv[0]] = kv[1];
                }
            }

            return header;
        }
    }

    public class NamedValue
    {
        public NamedValue(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name
        { get; }

        public string Value
        { get; }
    }
}