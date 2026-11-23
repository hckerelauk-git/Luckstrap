using Microsoft.Win32;
using Bloxstrap.AppData;

namespace Bloxstrap.Utility
{
    static class WindowsRegistry
    {
        private const string RobloxPlaceKey = "Roblox.Place";
        
        public static readonly List<RegistryKey> Roots = new() { Registry.CurrentUser, Registry.LocalMachine };

        public static void RegisterProtocol(string key, string name, string handler, string handlerParam = "%1")
        {
            string handlerArgs = $"\"{handler}\" {handlerParam}";

            using var uriKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{key}");
            using var uriIconKey = uriKey.CreateSubKey("DefaultIcon");
            using var uriCommandKey = uriKey.CreateSubKey(@"shell\open\command");

            if (uriKey.GetValue("") is null)
            {
                uriKey.SetValueSafe("", $"URL: {name} Protocol");
                uriKey.SetValueSafe("URL Protocol", "");
            }

            if (uriCommandKey.GetValue("") as string != handlerArgs)
            {
                uriIconKey.SetValueSafe("", handler);
                uriCommandKey.SetValueSafe("", handlerArgs);
            }
        }

        /// <summary>
        /// Registers Roblox Player protocols for Bloxstrap
        /// </summary>
        public static void RegisterPlayer()
        {
            // 设置里关掉了就不接管。这里统一拦一道，启动、安装、改设置三个调用点都不用各写一遍判断
            if (!App.Settings.Prop.InterceptBrowserLaunches)
            {
                RestorePlayer();
                return;
            }

            RegisterPlayer(Paths.Application, "-player \"%1\"");
        }

        public static void RegisterPlayer(string handler, string handlerParam)
        {
            RegisterProtocol("roblox", "Roblox", handler, handlerParam);
            RegisterProtocol("roblox-player", "Roblox", handler, handlerParam);
        }

        /// <summary>
        /// 把浏览器启动链接交回 Roblox 自己。找不到官方 launcher 就把协议键删掉，
        /// 浏览器点 Play 会提示找不到应用 —— 这比继续走我们这边更符合「关掉」的意思
        /// </summary>
        public static void RestorePlayer()
        {
            string? launcher = null;

            try
            {
                string? dir = Path.GetDirectoryName(new RobloxPlayerData().ExecutablePath);

                if (!String.IsNullOrEmpty(dir))
                {
                    string candidate = Path.Combine(dir, "RobloxPlayerLauncher.exe");

                    if (File.Exists(candidate))
                        launcher = candidate;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("WindowsRegistry::RestorePlayer", "Could not locate the Roblox launcher");
                App.Logger.WriteException("WindowsRegistry::RestorePlayer", ex);
            }

            if (launcher is null)
            {
                Unregister("roblox");
                Unregister("roblox-player");
                return;
            }

            RegisterPlayer(launcher, "\"%1\"");
        }

        /// <summary>
        /// Registers all Roblox Studio classes for Bloxstrap
        /// </summary>
        public static void RegisterStudio()
        {
            RegisterStudioProtocol(Paths.Application, "-studio \"%1\"");
            RegisterStudioFileClass(Paths.Application, "-studio \"%1\"");
            RegisterStudioFileTypes();
        }

        /// <summary>
        /// Registers roblox-studio and roblox-studio-auth protocols
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="handlerParam"></param>
        public static void RegisterStudioProtocol(string handler, string handlerParam)
        {
            RegisterProtocol("roblox-studio", "Roblox", handler, handlerParam);
            RegisterProtocol("roblox-studio-auth", "Roblox", handler, handlerParam);
        }

        /// <summary>
        /// Registers file associations for Roblox.Place class
        /// </summary>
        public static void RegisterStudioFileTypes()
        {
            RegisterStudioFileType(".rbxl");
            RegisterStudioFileType(".rbxlx");
        }

        /// <summary>
        /// Registers Roblox.Place class
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="handlerParam"></param>
        public static void RegisterStudioFileClass(string handler, string handlerParam)
        {
            const string keyValue = "Roblox Place";
            string handlerArgs = $"\"{handler}\" {handlerParam}";
            string iconValue = $"{handler},0";

            using RegistryKey uriKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + RobloxPlaceKey);
            using RegistryKey uriIconKey = uriKey.CreateSubKey("DefaultIcon");
            using RegistryKey uriOpenKey = uriKey.CreateSubKey(@"shell\Open");
            using RegistryKey uriCommandKey = uriOpenKey.CreateSubKey(@"command");

            if (uriKey.GetValue("") as string != keyValue)
                uriKey.SetValueSafe("", keyValue);

            if (uriCommandKey.GetValue("") as string != handlerArgs)
                uriCommandKey.SetValueSafe("", handlerArgs);

            if (uriOpenKey.GetValue("") as string != "Open")
                uriOpenKey.SetValueSafe("", "Open");

            if (uriIconKey.GetValue("") as string != iconValue)
                uriIconKey.SetValueSafe("", iconValue);
        }

        public static void RegisterStudioFileType(string key)
        {
            using RegistryKey uriKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{key}");
            uriKey.CreateSubKey(RobloxPlaceKey + @"\ShellNew");

            if (uriKey.GetValue("") as string != RobloxPlaceKey)
                uriKey.SetValueSafe("", RobloxPlaceKey);
        }

        public static void Unregister(string key)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{key}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("Protocol::Unregister", $"Failed to unregister {key}: {ex}");
            }
        }
    }
}
