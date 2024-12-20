using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ZZZConfigManager
{
    internal class Program
    {
        private static void PrintUsage()
        {
            Console.WriteLine("用法: ZZZConfigManager.exe [选项]");
            Console.WriteLine();
            Console.WriteLine("选项:");
            Console.WriteLine("  -h             显示此帮助信息");
            Console.WriteLine("  -p <路径>      指定游戏安装路径");
            Console.WriteLine("  -m <模式>      设置移动端UI模式 (0=关闭, 1=开启)");
            Console.WriteLine("  -q             安静模式 - 仅显示错误信息");
            Console.WriteLine();
            Console.WriteLine("无参数运行时会自动切换移动端UI的开启/关闭状态。");
            Console.WriteLine();
            Console.WriteLine("示例:");
            Console.WriteLine("  ZZZConfigManager.exe                   自动切换移动端UI开关状态");
            Console.WriteLine("  ZZZConfigManager.exe -m 1              开启移动端UI");
            Console.WriteLine("  ZZZConfigManager.exe -p \"D:\\Games\\ZenlessZoneZero Game\" -m 0  指定路径并关闭移动端UI");
            Console.WriteLine("  ZZZConfigManager.exe -q -m 1           静默开启移动端UI");
        }

        public static string GetGameInstallPath()
        {
            string registryPath = @"Software\miHoYo\HYP\1_1\nap_cn";
            string valueName = "GameInstallPath";

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath))
            {
                if (key != null)
                {
                    object value = key.GetValue(valueName);
                    return value?.ToString() ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private static int GetCurrentMobileMode(string configContent)
        {
            var match = Regex.Match(configContent, "LocalUILayoutPlatform\"\\s*:\\s*(\\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int currentMode))
            {
                return currentMode == 1 ? 1 : 2; // Ensure we only get 1 or 2
            }
            return 2; // Default to PC mode (2) if not found
        }

        static void Main(string[] args)
        {
            string customPath = null;
            int? mobileMode = null;
            bool quietMode = args.Contains("-q");

            // Check for help flag first
            if (args.Contains("-h") || args.Contains("--help") || args.Contains("/?"))
            {
                PrintUsage();
                return;
            }

            // Parse command line arguments
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-p" when i + 1 < args.Length:
                        customPath = args[i + 1];
                        i++; // Skip next argument as it's the path
                        break;
                    case "-m" when i + 1 < args.Length:
                        if (int.TryParse(args[i + 1], out int mode) && (mode == 0 || mode == 1))
                        {
                            mobileMode = mode;
                        }
                        else if (!quietMode)
                        {
                            Console.WriteLine("移动模式值无效。使用默认值(0)。");
                        }
                        i++; // Skip next argument as it's the mode value
                        break;
                    case "-q":
                        break; // Already handled
                    case string s when s.StartsWith("-") && s != "-p" && s != "-m" && s != "-q":
                        Console.WriteLine($"未知选项: {s}");
                        Console.WriteLine("使用 -h 查看帮助信息");
                        return;
                }
            }

            byte[] magic = new byte[] { 85, 110, 209, 150, 116, 209, 131, 206, 149, 110, 103, 105, 110, 208, 181, 46, 71, 208, 176, 109, 101, 206, 159, 98, 106, 101, 209, 129, 116 };

            // Determine the file path
            string basePath = customPath ?? GetGameInstallPath();
            if (string.IsNullOrEmpty(basePath))
            {
                Console.WriteLine("错误：无法确定游戏安装路径。");
                Console.WriteLine("请使用 -p 选项指定游戏安装路径，或使用 -h 查看帮助信息。");
                if (!quietMode) Console.ReadLine();
                return;
            }

            if (!quietMode) Console.WriteLine($"安装路径：{basePath}");

            string filePath = System.IO.Path.Combine(basePath, "ZenlessZoneZero_Data", "Persistent", "LocalStorage", "GENERAL_DATA.bin");

            if (!System.IO.File.Exists(filePath))
            {
                Console.WriteLine($"错误：未在以下路径找到文件：{filePath}");
                Console.WriteLine("请确认路径是否正确，或使用 -h 查看帮助信息。");
                if (!quietMode) Console.ReadLine();
                return;
            }

            string raw;

            try
            {
                raw = Sleepy.ReadString(filePath, magic);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误：{ex}。请确保游戏已关闭。");
                if (!quietMode) Console.ReadLine();
                return;
            }

            // If no mode specified, determine it from current state
            if (!mobileMode.HasValue)
            {
                int currentMode = GetCurrentMobileMode(raw);
                mobileMode = currentMode == 2 ? 1 : 0; // Toggle the mode
                if (!quietMode) Console.WriteLine($"当前UI模式：{(currentMode == 1 ? "移动端" : "PC端")}");
            }

            // Update LocalUILayoutPlatform based on mobile mode
            int targetPlatform = mobileMode.Value == 1 ? 1 : 2;
            raw = Regex.Replace(
                raw,
                "(LocalUILayoutPlatform\")(\\s*:\\s*)(\\d+)",
                m => $"{m.Groups[1].Value}{m.Groups[2].Value}{targetPlatform}"
            );

            try
            {
                Sleepy.WriteString(filePath, raw, magic);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误：{ex}。请确保游戏已关闭。");
                if (!quietMode) Console.ReadLine();
                return;
            }

            if (!quietMode)
            {
                Console.WriteLine($"配置已更新。移动端UI：{(mobileMode.Value == 1 ? "开启" : "关闭")}");
                Console.WriteLine("按回车键退出...");
                Console.ReadLine();
            }
        }
    }
}