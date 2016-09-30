using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace EZShield.Incapsula.LogRelay
{
    /// <summary>
    /// Configuration settings for the "Connector" as defined in the <c>Settings.Config</c>
    /// file that you can retrieve from the Incapsula Log Management.
    /// </summary>
    public class ConnectorSettings : IEnumerable<Tuple<string,  object>>
    {

        public const string DEFAULT_PROCESS_DIR = ".\\siem_logs";

        //---------------------------------------
        // DO NOT MODIFY THESE PROPERTY NAMES!!!
        //---------------------------------------

        // These property names are named intentionally to match
        // the existing format of the Settings.Config as defined
        // by the Incapsula Log Management portal -- the names
        // should not be changed, and the properties should not
        // be added or removed unless it is to match changes in
        // the file format provided by the Log Management portal

        public string ApiId
        { get; set; }

        public string ApiKey
        { get; set; }

        /// <summary>
        /// Read-only configuration setting to return a masked variation of the
        /// <see cref="ApiKey"/> property, useful in debugging and logging.
        /// </summary>
        public string ApiKey_MASKED
        {
            get
            {
                return string.IsNullOrEmpty(ApiKey)
                    ? ""
                    : Regex.Replace(ApiKey, "[0-9a-xA-X]", "X"); 
            }
        }
        public string Process_Dir
        { get; set; } = DEFAULT_PROCESS_DIR;

        public string BaseUrl
        { get; set; }

        public string UseProxy
        { get; set; }

        public string ProxyServer
        { get; set; }

        public string Save_Locally
        { get; set; }

        public string Syslog_Enable
        { get; set; }

        public string Syslog_Address
        { get; set; }

        public string Syslog_Port
        { get; set; }

        public string Use_Custom_CA_File
        { get; set; }

        public string Custom_CA_File
        { get; set; }

        /// <summary>
        /// Returns an enumeration of significant settings and their values:
        /// <list type="bullet">
        ///   <item>ApiId</item>
        ///   <item>ApiKey_MASKED</item>
        ///   <item>Process_Dir</item>
        ///   <item>BaseUrl</item>
        /// </list>
        /// </summary>
        public IEnumerator<Tuple<string, object>> GetEnumerator()
        {
            return AppSettings.GetPropertyValues(this, new string[] {
                nameof(ApiId),
                nameof(ApiKey_MASKED),
                nameof(Process_Dir),
                nameof(BaseUrl),
            }).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
