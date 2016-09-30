using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace EZShield.Incapsula.LogRelay
{
    public class AppSettings : IEnumerable<Tuple<string, object>>
    {
        private static readonly NLog.Logger LOG = NLog.LogManager.GetCurrentClassLogger();

        public const string DEFAULT_RELAY_TARGET_CONFIG_FILE = "nlog-relay-target.config";

        public bool CreateProcessDir
        { get; set; } = true;

        public string ProxyUrl
        { get; set; }

        public int PullRetryCount
        { get; set; } = 3;

        public int PullRetrySleep
        { get; set; } = 30;

        public int PushRetryCount
        { get; set; } = 3;

        public int PushRetrySleep
        { get; set; } = 30;

        /// <summary>
        /// If enabled, will parse each line of the body of each log file pulled as a CEF
        /// record and process it accordingly.
        /// </summary>
        /// <remarks>
        /// The spec for the CEF format can be found at
        // <see cref="https://protect724.hp.com/docs/DOC-1072"/>.
        // <para>
        // The general format of each record is as follows:
        // <code>
        // CEF:Version|Device-Vendor|Device-Product|Device-Version|Device-Event-Class-ID|Name|Severity|[Extension]
        // </code>
        // </para><para>
        // When parsed, each line is pushed to the relay target via NLog logging calls.
        // The calls are made by translating each CEF line's severity to a corresponding
        // NLog Level using the following mapping:
        // <code>
        //         CEF Severity     |  | NLog
        //       Number | String    |=>| Severity
        //      --------+-----------+--+----------
        //              | Unknown   |=>| Trace
        //        0-3   | Low       |=>| Debug
        //        4-6   | Medium,   |=>| Info
        //        7-8   | High,     |=>| Warn
        //        9-10  | Very-High |=>| Error
        // </code>
        // Additionally, the CEF fields <c>Device-Vendor</c>, <c>Device-Product</c>
        // and <c>Name</c> will be extrapolated and formed into a NLog logger name
        // (each field concatenated in order separated by a period (.)).  With the
        // combination of log name and log level, it is possible to route the log
        // records and filter them as you see fit.
        // </para>
        //
        /// </remarks>
        public bool ParseCEF
        { get; set; }

        public string RelayTargetConfig
        { get; set; } = DEFAULT_RELAY_TARGET_CONFIG_FILE;

        public IDictionary<string, string> EncryptionKeys
        { get; set; }
        
        public ConnectorSettings ConnectorSettings
        { get; } = new ConnectorSettings();


        public IEnumerator<Tuple<string, object>> GetEnumerator()
        {
            return GetPropertyValues(this, new string[] {
                nameof(CreateProcessDir),
                nameof(ProxyUrl),
                nameof(ConnectorSettings),
            }).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public static IEnumerable<Tuple<string, object>> GetPropertyValues(object obj,
                IEnumerable<string> propNames, string prefix = null, Type type = null)
        {
            if (type == null)
                type = obj.GetType();
            var ti = type.GetTypeInfo();

            foreach (var pn in propNames)
            {
                var prop = ti.GetProperty(pn);
                if (prop == null)
                {
                    LOG.Warn("missing or invalid property name [{0}] on type [{1}]", pn, ti.FullName);
                    continue;
                }

                var propVal = prop.GetValue(obj);

                if (propVal != null
                        && typeof(IEnumerable<Tuple<string, object>>).IsInstanceOfType(propVal))
                {
                    foreach (var pv in (IEnumerable<Tuple<string, object>>)propVal)
                        yield return Tuple.Create($"{prefix}{pn}:{pv.Item1}", pv.Item2);
                }
                else
                {
                    yield return Tuple.Create($"{prefix}{pn}", propVal);
                }
            }
        }
    }
}