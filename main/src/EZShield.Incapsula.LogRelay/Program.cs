using System.Reflection;

namespace EZShield.Incapsula.LogRelay
{
    /// <summary>
    /// Entry-point into running this tool from the CLI.
    /// </summary>
    public class Program
    {
        private static readonly NLog.Logger LOG = NLog.LogManager.GetCurrentClassLogger();

        public static App _app;

        public static void Main(string[] args)
        {
            LOG.Info("================================");
            LOG.Info("== Starting up");
            LOG.Info("================================");

            LOG.Info("Current working directory:  [{0}]",
                    System.AppContext.BaseDirectory);
            LOG.Info("App Assembly Base:  [{0}]",
                    typeof(App).GetTypeInfo().Assembly.CodeBase);

            _app = new App();

            LOG.Info("Loading configuration settings");
            _app.Config(args);

            LOG.Info("Initializing app");
            _app.Init();

            LOG.Info("Running app");
            _app.Run();
        }
    }
}
