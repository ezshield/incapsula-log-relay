using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace EZShield.Incapsula.LogRelay
{
    public class App
    {
        private static readonly NLog.Logger LOG = NLog.LogManager.GetCurrentClassLogger();

        public const string ENV_CONFIG_PREFIX = "INCAP_LOGS_RELAY_";

        public const string FETCH_INDEX_FILE = "logs.index";

        public const string LOG_BODY_MARKER = "|==|\n";

        // We cache a byte array version of the body marker to improve performance
        private static readonly byte[] LOG_BODY_MARKER_BYTES =
                Encoding.ASCII.GetBytes(LOG_BODY_MARKER);
        private static readonly int LOG_BODY_MARKER_LENGTH = LOG_BODY_MARKER_BYTES.Length;

        private IConfigurationRoot _config;

        private NLog.Config.XmlLoggingConfiguration _targetConfig;
        private NLog.LogFactory _targetFactory;
        private NLog.Logger _target;

        private AppState _state;
        private string _stateFilePath;

        private LogFetcher _fetcher;

        public AppSettings Settings
        { get; } = new AppSettings();

        /// <summary>
        /// Load configuration settings from all the possible sources we support:
        /// <list>
        ///   <item>JSON config file (IncapLogRelay.config)</item>
        ///   <item>INI config file (Settings.config - Connector settings only)</item>
        ///   <item>User Secrets (.NET Core DEV-time support)</item>
        ///   <item>Environment Vars (prefix with INCAP_LOGS_RELAY_)</item>
        ///   <item>CLI args</item>
        /// </list>
        /// </summary>
        /// <param name="cliArgs"></param>
        public void Config(string[] cliArgs = null)
        {
            var configBuilder = new ConfigurationBuilder()
                //.SetBasePath(System.AppContext.BaseDirectory)
                .AddJsonFile("IncapLogRelay.config", true, false)
                .AddIniFile("Settings.Config", true, false)
                .AddUserSecrets("IncapLogRelay")
                .AddEnvironmentVariables(ENV_CONFIG_PREFIX)
                ;

            if (cliArgs?.Length > 0)
                configBuilder.AddCommandLine(cliArgs);

            _config = configBuilder.Build();
            _config.GetSection("Settings")?.Bind(Settings.ConnectorSettings);
            _config.Bind(Settings);

            LOG.Debug("Settings:");
            foreach (var s in Settings)
                LOG.Debug("  * [{0}] = [{1}]",
                        s.Item1.EndsWith("_MASKED") ? s.Item1.Replace("_MASKED", " (MASKED)") : s.Item1,
                        s.Item2);

            // Make sure there's a file for the relay target NLog configuration
            if (!File.Exists(Settings.RelayTargetConfig))
            {
                LOG.Fatal("NLog configuration for relay target could not be found at:  [{0}]",
                        Settings.RelayTargetConfig);
                throw new FileNotFoundException("missing relay target configuration");
            }
        }

        public void Init()
        {
            if (!Directory.Exists(Settings.ConnectorSettings.Process_Dir)
                    && Settings.CreateProcessDir)
                Directory.CreateDirectory(Settings.ConnectorSettings.Process_Dir);

            _stateFilePath = Path.Combine(Settings.ConnectorSettings.Process_Dir, ".state");
            LOG.Info("State file path:  [{0}]", _stateFilePath);
            if (File.Exists(_stateFilePath))
            {
                LOG.Info("Loading existing state file");
                _state = AppState.Load(_stateFilePath);
            }
            else
            {
                LOG.Info("No existing state file found; starting new");
                _state = new AppState();
            }
            _state.StartupTime = DateTime.Now;
            SaveState();

            _fetcher = new LogFetcher(
                    new Uri(Settings.ConnectorSettings.BaseUrl),
                    Settings.ConnectorSettings.ApiId,
                    Settings.ConnectorSettings.ApiKey);

            if (!string.IsNullOrEmpty(Settings.ProxyUrl))
                _fetcher.Proxy = new Uri(Settings.ProxyUrl);

            LOG.Info("Building NLog configuration for relay target");
            _targetFactory = new NLog.LogFactory();
            _targetConfig = new NLog.Config.XmlLoggingConfiguration(
                    Settings.RelayTargetConfig, _targetFactory);
            if (!_targetConfig.InitializeSucceeded.GetValueOrDefault(false))
            {
                LOG.Fatal("relay target NLog configuration failed to initialize");
                throw new InvalidDataException("unable to configure relay target");
            }
            var allTargets = _targetConfig.AllTargets.ToArray();
            if (allTargets.Length == 0)
            {
                LOG.Warn("No logging targets configured!");
            }
            else
            {
                LOG.Info("Relay Target(s) configured with:");
                foreach (var t in allTargets)
                    LOG.Info("  * {0}", t.Name);
            }
        }

        public void Run()
        {
            RunCycle();
        }

        public void RunCycle()
        {
            try
            {
                _state.LastCycleTime = DateTime.Now;
                _state.LastCycleError = null;
                var logFileNames = FetchIndexFile();
                foreach (var logFileName in logFileNames)
                {
                    if (string.IsNullOrWhiteSpace(logFileName))
                        continue;

                    LogFileState logFileState;
                    if (!_state.LogFiles.TryGetValue(logFileName, out logFileState))
                    {
                        logFileState = new LogFileState();
                        _state.LogFiles[logFileName] = logFileState;
                    }

                    // Skip if already pushed or exceeded the push retry count
                    if (logFileState.PushTryTime != null
                            || logFileState.PushTryCount >= Settings.PushRetryCount)
                        continue;

                    if (logFileState.PullTime == null)
                    {
                        // Skip if already exceeded pull retry count
                        if (logFileState.PullTryCount >= Settings.PullRetryCount)
                            continue;

                        logFileState.PullTryTime = DateTime.Now;
                        ++logFileState.PullTryCount;
                        SaveState();
                        try
                        {
                            FetchLogFile(logFileName);
                            logFileState.PullTime = DateTime.Now;
                            SaveState();
                        }
                        catch (Exception ex)
                        {
                            LOG.Error(ex, "Failed to PULL log file [{0}]", logFileName);
                            logFileState.PullError = ex.Message;
                            SaveState();
                        }
                    }

                    try
                    {
                        LOG.Warn("TODO: IMPLEMENT PUSH");
                        logFileState.PushTryTime = DateTime.Now;
                        ++logFileState.PushTryCount;
                        SaveState();

                        // TODO: PUSH
                        logFileState.PushTime = DateTime.Now;
                        SaveState();
                    }
                    catch (Exception ex)
                    {
                        LOG.Error(ex, "Failed to PUSH log file [{0}]", logFileName);
                        logFileState.PushError = ex.Message;
                        SaveState();
                    }

                    break;
                }
            }
            catch (Exception ex)
            {
                LOG.Error(ex, "Failed to complete cycle");
                _state.LastCycleError = ex.Message;
                SaveState();
            }
        }

        public IEnumerable<string> FetchIndexFile()
        {
            LOG.Debug("Fetching index file [{0}]", FETCH_INDEX_FILE);
            var index = _fetcher.FetchString(FETCH_INDEX_FILE);
            LOG.Debug("Retrieved index file of size [{0}]", index.Length);
            File.WriteAllText(
                    Path.Combine(Settings.ConnectorSettings.Process_Dir, FETCH_INDEX_FILE), index);

            return index.Split();
        }

        public void FetchLogFile(string logName)
        {
            LOG.Debug("Fetching log file [{0}]", logName);
            var logRaw = _fetcher.FetchBytes(logName);
            LOG.Debug("Retrieved log file of size [{0}]", logRaw.Length);

            File.WriteAllBytes(
                    Path.Combine(Settings.ConnectorSettings.Process_Dir, logName), logRaw);

            ProcessLogFile(logName, logRaw);
        }

        public class LogFileContent
        {
            public byte[] _rawLog;
            public string _logHeader;
        }

        public void ProcessLogFile(string logName, byte[] logRaw)
        {
            var split = SplitLogHeaderFromBody(logRaw);
            if (split == null)
                LOG.Warn("Unable to parse out header and body");

            LOG.Debug("Saving extracted header");
            File.WriteAllText(
                    Path.Combine(Settings.ConnectorSettings.Process_Dir, logName + ".hdr"),
                    split.Item1);

            LOG.Debug("Parsing header");
            var header = LogFileHeader.Parse(split.Item1);
            if (header.GetUnknownCount() > 0)
            {
                LOG.Warn("Header contained invalid or unknown entries");
                foreach (var kv in header)
                {
                    LOG.Debug("  * {0} = {1}", kv.Name, kv.Value);
                }
            }
            else if (LOG.IsDebugEnabled)
            {
                LOG.Debug("Log header:  {0}", JsonConvert.SerializeObject(header));
            }

            byte[] logBodyBytes;
            if (string.IsNullOrEmpty(header.Key))
            {
                LOG.Debug("Decompressing body");
                using (var msIn = new MemoryStream(logRaw, split.Item2, split.Item3))
                {
                    using (var msOut = new MemoryStream())
                    {
                        ZLib.Decompress(msIn, msOut);
                        logBodyBytes = msOut.ToArray();
                    }
                }
                File.WriteAllBytes(
                        Path.Combine(Settings.ConnectorSettings.Process_Dir, logName + ".bdy"),
                        logBodyBytes);
                LOG.Debug("Saved extracted/decompressed body");
            }
            else
            {
                throw new NotImplementedException("encryption key");
            }

            if (!string.IsNullOrEmpty(header.Checksum))
            {
                var checksum = ComputeChecksum(logBodyBytes);
                if (!header.Checksum.Equals(checksum, StringComparison.OrdinalIgnoreCase))
                {
                    LOG.Warn("Failed checksum:  expected=[{0}]; found=[{1}]",
                            header.Checksum, checksum);
                }
                else
                {
                    LOG.Debug("Checksum validated");
                }
            }
        }

        private void SaveState()
        {
            _state.Save(_stateFilePath);
        }

        public static Tuple<string, int, int> SplitLogHeaderFromBody(byte[] logRaw)
        {
            var logLength = logRaw.Length;
            var searchLimit = logLength - LOG_BODY_MARKER_LENGTH;

            for (var i = 0; i < searchLimit; ++i)
            {
                var k = 0;
                for (; k < LOG_BODY_MARKER_LENGTH; ++k)
                {
                    if (LOG_BODY_MARKER_BYTES[k] != logRaw[i + k])
                        break;
                }

                if (k == LOG_BODY_MARKER_LENGTH)
                {
                    return Tuple.Create(
                        // The header extracted as a string
                        Encoding.ASCII.GetString(logRaw, 0, i),
                        // Index into raw log bytes of the start of the body
                        i + LOG_BODY_MARKER_LENGTH,
                        // Length of the raw bytes of the body
                        logLength - (i + LOG_BODY_MARKER_LENGTH)
                    );
                }
            }

            return null;
        }

        public static string ComputeChecksum(byte[] data)
        {
            using (var md = System.Security.Cryptography.MD5.Create())
            {
                var hash = md.ComputeHash(data);
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }
    }
}