using System;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Security.Principal;
using System.Text;

namespace WindowsServiceManagement
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            // Assembly からバージョンを取得
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";

            var cfg = ServiceConfig.Load();
            Console.WriteLine($"=== Windows サービス操作ツール v{version}（cmd.exe /c 使用）===");
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
                Console.WriteLine("[8] SeServiceLogon 付与  [9] services.msc を開く");
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
                        case "8": GrantLogonAsService(cfg); break;
                        case "9": OpenServicesConsole(); break;
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

        static void GrantLogonAsService(ServiceConfig cfg)
        {
            var acct = cfg.ServiceUser;
            if (string.IsNullOrWhiteSpace(acct))
            {
                Console.Write("付与するアカウント（例 .\\svcUser / DOMAIN\\user）：");
                acct = Console.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(acct))
                {
                    Console.WriteLine("アカウントが未指定です。");
                    return;
                }
            }

            if (!Confirm($"アカウント {acct} に『サービスとしてログオン』権限を付与しますか？"))
                return;

            try
            {
                UserRightsUtil.AddLogonAsService(acct);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"OK: {acct} に SeServiceLogonRight を付与しました。");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("付与に失敗: " + ex.Message);
                Console.ResetColor();
                Console.WriteLine("※ 管理者権限で実行しているか、ドメイングループポリシーで禁止されていないか確認してください。");
            }
        }

        static void OpenServicesConsole()
        {
            if (!Confirm("Windows サービス管理コンソール（services.msc）を開きますか？"))
                return;

            var psi = new ProcessStartInfo
            {
                FileName = "mmc.exe",
                Arguments = "services.msc",
                UseShellExecute = true,  // ここはtrue（GUIを開くため）
            };

            try
            {
                Process.Start(psi);
                Console.WriteLine("services.msc を開きました。");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] services.msc を開けませんでした: {ex.Message}");
                Console.ResetColor();
            }
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



    static class UserRightsUtil
    {
        public static void AddLogonAsService(string accountName)
        {
            // 1) アカウント名 → SID
            var nt = new NTAccount(accountName);
            var sid = (SecurityIdentifier)nt.Translate(typeof(SecurityIdentifier));
            byte[] sidBytes = new byte[sid.BinaryLength];
            sid.GetBinaryForm(sidBytes, 0);

            // 2) LSA ポリシーを開く
            LSA_OBJECT_ATTRIBUTES loa = default;
            IntPtr policy;
            uint access = POLICY_LOOKUP_NAMES | POLICY_CREATE_ACCOUNT;
            var status = LsaOpenPolicy(IntPtr.Zero, ref loa, access, out policy);
            ThrowOnLsaError(status, "LsaOpenPolicy");

            try
            {
                // 3) 権限名（Unicode）
                var right = new LSA_UNICODE_STRING("SeServiceLogonRight");

                // 4) 付与
                status = LsaAddAccountRights(policy, sidBytes, new[] { right }, 1);
                ThrowOnLsaError(status, "LsaAddAccountRights");
            }
            finally
            {
                if (policy != IntPtr.Zero) LsaClose(policy);
            }
        }

        // ==== P/Invoke ====
        const uint POLICY_CREATE_ACCOUNT = 0x00000010;
        const uint POLICY_LOOKUP_NAMES = 0x00000800;

        [StructLayout(LayoutKind.Sequential)]
        struct LSA_OBJECT_ATTRIBUTES
        {
            public int Length;
            public IntPtr RootDirectory;
            public IntPtr ObjectName; // PLSA_UNICODE_STRING
            public uint Attributes;
            public IntPtr SecurityDescriptor;
            public IntPtr SecurityQualityOfService;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct LSA_UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;

            public LSA_UNICODE_STRING(string s)
            {
                if (s == null) s = string.Empty;
                var bytes = System.Text.Encoding.Unicode.GetBytes(s);
                Length = (ushort)bytes.Length;
                MaximumLength = (ushort)(Length + 2);
                Buffer = Marshal.StringToHGlobalUni(s);
            }
        }

        [DllImport("advapi32.dll")]
        static extern uint LsaOpenPolicy(IntPtr SystemName, ref LSA_OBJECT_ATTRIBUTES ObjectAttributes, uint DesiredAccess, out IntPtr PolicyHandle);

        [DllImport("advapi32.dll")]
        static extern uint LsaAddAccountRights(IntPtr PolicyHandle, byte[] AccountSid, LSA_UNICODE_STRING[] UserRights, int CountOfRights);

        [DllImport("advapi32.dll")]
        static extern uint LsaClose(IntPtr ObjectHandle);

        [DllImport("advapi32.dll")]
        static extern uint LsaNtStatusToWinError(uint Status);

        static void ThrowOnLsaError(uint ntStatus, string api)
        {
            if (ntStatus == 0) return;
            uint winErr = LsaNtStatusToWinError(ntStatus);
            throw new System.ComponentModel.Win32Exception((int)winErr, $"{api} 失敗 (0x{ntStatus:X8}, Win32={winErr})");
        }
    }

}
