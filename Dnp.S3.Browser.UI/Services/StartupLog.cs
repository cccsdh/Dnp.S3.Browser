using Microsoft.Extensions.Logging;
using System;

namespace Dnp.S3.Browser.UI.Services
{
    internal static class StartupLog
    {
        private static bool _loaded = false;
        private static bool _enabled = false;

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            var v = Environment.GetEnvironmentVariable("DNP_S3_STARTUP_LOG");
            _enabled = !string.IsNullOrEmpty(v) && (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
            _loaded = true;
        }

        public static bool Enabled
        {
            get { EnsureLoaded(); return _enabled; }
        }

        public static void Log(ILogger? logger, string message)
        {
            if (!Enabled) return;
            try
            {
                if (logger != null)
                {
                    logger.LogDebug(message);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(message);
                }
            }
            catch { }
        }

        public static void Log(string message)
        {
            if (!Enabled) return;
            try { System.Diagnostics.Debug.WriteLine(message); } catch { }
        }
    }
}
