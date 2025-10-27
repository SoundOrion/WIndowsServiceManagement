using System;
using System.Configuration;
using System.Diagnostics;
using System.Text;

namespace WindowsServiceManagement
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var cfg = ServiceConfig.Load();
            Console.WriteLine("=== Windows サービス操作ツール（cmd.exe /c 使用）===");
            Console.WriteLine($"ServiceName : {cfg.ServiceName}");
            Console.WriteLine($"DisplayName : {cfg.DisplayName}");
            Console.WriteLine($"BinPath     : {cfg.BinPath}");
            Console.WriteLine($"StartupType : {cfg.StartupType}");
            if (!string.IsNullOrWhiteSpace(cfg.ServiceUser))
                Console.WriteLine($"RunAs       : {cfg.ServiceUser}");
            Console.WriteLine();

            while (true)
            {
                Console.WriteLine("[1] 登録(create)  [2] 起動(start)  [3] 停止(stop)  [4] 削除(delete)");
                Console.WriteLine("[5] 状態(query)   [6] 説明設定(description)  [7] 自動/手動 切替");
                Console.WriteLine("[0] 終了");
                Console.Write("選択: ");
                var key = Console.ReadLine()?.Trim();

                try
                {
                    switch (key)
                    {
                        case "1": CreateService(cfg); break;
                        case "2": StartService(cfg); break;
                        case "3": StopService(cfg); break;
                        case "4": DeleteService(cfg); break;
                        case "5": QueryService(cfg); break;
                        case "6": SetDescription(cfg); break;
                        case "7": SetStartupType(cfg); break;
                        case "0":
                            Console.WriteLine("終了します。何かキーで閉じます…");
                            Console.ReadKey(true);
                            return;
                        default:
                            Console.WriteLine("無効な選択です。");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[ERROR] " + ex.Message);
                    Console.ResetColor();
                }

                Console.WriteLine();
            }
        }

        // ====== 操作 ======
        static void CreateService(ServiceConfig cfg)
        {
            // sc create では = の後のスペースが必須
            string args = $"sc create {Q(cfg.ServiceName)} binPath= {Q(cfg.BinPath)} start= {cfg.StartupType} DisplayName= {Q(cfg.DisplayName)}";
            if (!string.IsNullOrWhiteSpace(cfg.Dependencies))
                args += $" depend= {Q(cfg.Dependencies)}"; // 例: "Tcpip/Afd"

            if (!Confirm($"サービス登録しますか？\n  {args}"))
                return;

            RunCmd(args);

            // 実行アカウントを指定する場合（任意）
            if (!string.IsNullOrWhiteSpace(cfg.ServiceUser))
            {
                string cfgArgs = $"sc config {Q(cfg.ServiceName)} obj= {Q(cfg.ServiceUser)} password= {Q(cfg.ServicePassword ?? "")}";
                RunCmd(cfgArgs);
            }

            // 説明を設定（任意）
            if (!string.IsNullOrWhiteSpace(cfg.Description))
            {
                string descArgs = $"sc description {Q(cfg.ServiceName)} {Q(cfg.Description)}";
                RunCmd(descArgs);
            }
        }

        static void StartService(ServiceConfig cfg)
        {
            string args = $"net start {Q(cfg.ServiceName)}";
            if (!Confirm($"サービスを起動しますか？\n  {args}")) return;
            RunCmd(args);
        }

        static void StopService(ServiceConfig cfg)
        {
            string args = $"net stop {Q(cfg.ServiceName)}";
            if (!Confirm($"サービスを停止しますか？\n  {args}")) return;
            RunCmd(args);
        }

        static void DeleteService(ServiceConfig cfg)
        {
            string args = $"sc delete {Q(cfg.ServiceName)}";
            if (!Confirm($"サービスを削除しますか？\n  {args}")) return;
            RunCmd(args);
        }

        static void QueryService(ServiceConfig cfg)
        {
            string args = $"sc query {Q(cfg.ServiceName)}";
            RunCmd(args);
            // 詳細が欲しければ sc qc も
            RunCmd($"sc qc {Q(cfg.ServiceName)}");
        }

        static void SetDescription(ServiceConfig cfg)
        {
            if (string.IsNullOrWhiteSpace(cfg.Description))
            {
                Console.Write("説明を入力してください：");
                cfg.Description = Console.ReadLine() ?? "";
            }
            string args = $"sc description {Q(cfg.ServiceName)} {Q(cfg.Description)}";
            if (!Confirm($"説明を設定しますか？\n  {args}")) return;
            RunCmd(args);
        }

        static void SetStartupType(ServiceConfig cfg)
        {
            Console.Write("StartupType を入力（auto/demand/disabled）: ");
            var v = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
            if (v != "auto" && v != "demand" && v != "disabled")
            {
                Console.WriteLine("不正な値です。");
                return;
            }
            string args = $"sc config {Q(cfg.ServiceName)} start= {v}";
            if (!Confirm($"起動種別を変更しますか？\n  {args}")) return;
            RunCmd(args);
        }

        // ====== 共通 ======
        static (int code, string stdout, string stderr) RunCmd(string rawArgs)
        {
            var comspec = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            var psi = new ProcessStartInfo
            {
                FileName = comspec,
                Arguments = "/c " + rawArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            Console.WriteLine($"> {psi.FileName} {psi.Arguments}");
            using (var p = Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd();
                string e = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (!string.IsNullOrWhiteSpace(o))
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine(o.TrimEnd());
                    Console.ResetColor();
                }
                if (!string.IsNullOrWhiteSpace(e))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[stderr]");
                    Console.WriteLine(e.TrimEnd());
                    Console.ResetColor();
                }
                Console.WriteLine($"[ExitCode] {p.ExitCode}");
                return (p.ExitCode, o, e);
            }
        }

        static bool Confirm(string message)
        {
            Console.WriteLine(message);
            Console.Write("実行しますか？ (y/N): ");
            var ans = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
            return ans == "y" || ans == "yes";
        }

        static string Q(string s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            // 引数全体を二重引用符で囲む（内部の " はエスケープ）
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }

    class ServiceConfig
    {
        public string ServiceName { get; set; }
        public string DisplayName { get; set; }
        public string BinPath { get; set; }
        public string StartupType { get; set; } = "auto";  // auto|demand|disabled
        public string Description { get; set; }
        public string Dependencies { get; set; }           // 例: "Tcpip/Afd"
        public string ServiceUser { get; set; }            // 例: ".\\MyUser" or "DOMAIN\\User"
        public string ServicePassword { get; set; }

        public static ServiceConfig Load()
        {
            string Get(string key) => ConfigurationManager.AppSettings[key];

            return new ServiceConfig
            {
                ServiceName = Get("ServiceName") ?? throw new Exception("App.config: ServiceName が未設定"),
                DisplayName = Get("DisplayName") ?? Get("ServiceName") ?? "My Service",
                BinPath = Get("BinPath") ?? throw new Exception("App.config: BinPath が未設定"),
                StartupType = (Get("StartupType") ?? "auto").ToLowerInvariant(),
                Description = Get("Description") ?? "",
                Dependencies = Get("Dependencies") ?? "",
                ServiceUser = Get("ServiceUser") ?? "",
                ServicePassword = Get("ServicePassword") ?? ""
            };
        }
    }
}
