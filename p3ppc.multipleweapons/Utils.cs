using p3ppc.multipleweapons.Configuration;
using Reloaded.Memory.Sigscan.Definitions.Structs;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using System;
using System.Diagnostics;
using System.Text;

namespace p3ppc.multipleweapons
{

    internal static class Utils
    {
        private static ILogger _logger = null!;
        private static Config _config = null!;
        private static IStartupScanner _startupScanner = null!;
        internal static nint BaseAddress { get; private set; }

        internal static bool Initialise(ILogger logger, Config config, IModLoader modLoader)
        {
            _logger = logger;
            _config = config;
            using var thisProcess = Process.GetCurrentProcess();
            BaseAddress = thisProcess.MainModule!.BaseAddress;

            var startupScannerController = modLoader.GetController<IStartupScanner>();
            if (startupScannerController == null || !startupScannerController.TryGetTarget(out _startupScanner))
            {
                LogError("The startup scan tool is not ready.");
                return false;
            }

            return true;
        }

        internal static void UpdateConfiguration(Config config) => _config = config;

        internal static void LogDebug(string message)
        {
            if (_config.DebugEnabled)
            {
                _logger.WriteLine($"[Multiple Weapons] {message}");
            }
        }

        internal static void Log(string message)
        {
            _logger.WriteLine($"[Multiple Weapons] {message}");
        }

        internal static void LogError(string message, Exception e)
        {
            _logger.WriteLine($"[Multiple Weapons] ERROR: {message}: {e.Message}", System.Drawing.Color.Red);
        }

        internal static void LogError(string message)
        {
            _logger.WriteLine($"[Multiple Weapons] ERROR: {message}", System.Drawing.Color.Red);
        }

        internal static void SigScan(string pattern, string name, Action<nint> action)
        {
            _startupScanner.AddMainModuleScan(pattern, result =>
            {
                if (!result.Found)
                {
                    LogError($"Could not find {name}.");
                    return;
                }
                LogDebug($"Resolved {name}.");

                action(result.Offset + BaseAddress);
            });
        }

        internal static unsafe nint ResolveRelativeCall(nint instruction)
        {
            if (*(byte*)instruction != 0xE8)
            {
                throw new InvalidOperationException("Expected a relative CALL instruction.");
            }

            return instruction + 5 + *(int*)(instruction + 1);
        }

        internal static unsafe nuint GetGlobalAddress(nint ptrAddress) => (nuint)(*(int*)ptrAddress + ptrAddress + 4);
    }
}
