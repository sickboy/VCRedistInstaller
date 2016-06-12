using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace VCRedistInstaller
{
    public enum VCRedists
    {
        VS2012 = 11,
        VS2013 = 12,
        VS2015 = 14
    }

    public class VcRedistInstaller
    {
        public async Task<VcRedistInfo[]> Checker(params VcRedistInfo[] versions)
            => versions.Where(v => !CheckVcRedistInstalled(v)).ToArray();

        public async Task DownloadAndInstall(params VcRedistInfo[] versions)
        {
            using (var bc = new BlockingCollection<string>())
            {
                var t = Task.Factory.StartNew(async () =>
                {
                    foreach (var c in bc.GetConsumingEnumerable())
                    {
                        await Install(c);
                    }
                }).Unwrap();
                foreach (var v in versions)
                {
                    var fileName = Path.Combine(Path.GetTempPath(), $"vcredist_x86-{v}.exe");
                    await DownloadVersion(fileName, v.Url);
                    bc.Add(fileName);
                }
                bc.CompleteAdding();
                await t;
            }
        }

        private Task Install(string fileName) => Task.Factory.StartNew(() =>
        {
            using (var p = Process.Start(fileName, "/q /norestart"))
                p.WaitForExit();
            using (var p = Process.Start(fileName, "/q /repair /norestart"))
                p.WaitForExit();
        });

        private async Task DownloadVersion(string fileName, string url)
        {
            using (var webClient = new WebClient())
            {
                await webClient.DownloadFileTaskAsync(url, fileName);
            }
        }

        private static bool CheckVcRedistInstalled(VcRedistInfo info, RegistryView bit = RegistryView.Registry32)
            => VerifyRegistry(info, bit) && VerifySystemFile(info, bit);

        private static bool VerifySystemFile(VcRedistInfo info, RegistryView bit)
        {
            switch (bit)
            {
                case RegistryView.Registry32:
                {
                    var folder = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
                    return File.Exists(Path.Combine(folder, info.SystemFile));
                }
                case RegistryView.Registry64:
                {
                    var folder = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    return File.Exists(Path.Combine(folder, info.SystemFile));
                }
                // TODO: This would be 64-bit on 64-bit runtime, and 32-bit on 32-bit runtime :S
                case RegistryView.Default:
                    throw new NotSupportedException(bit + " is not supported");
                default:
                    throw new NotSupportedException(bit + " is not supported");
            }
        }

        private static bool VerifyRegistry(VcRedistInfo info, RegistryView bit)
        {
            var regVersion = $"{(int) info.Version}.0";
            using (var reg = OpenRegistry(bit)
                .OpenSubKey($@"SOFTWARE\Microsoft\DevDiv\vc\Servicing\{regVersion}\RuntimeMinimum"))
            {
                if (reg != null && TryConfirm(reg))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TryConfirm(RegistryKey reg)
        {
            try
            {
                return Convert.ToInt64(reg.GetValue("Install")) == 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
        }

        static RegistryKey OpenRegistry(RegistryView bit = RegistryView.Registry32,
            RegistryHive hive = RegistryHive.LocalMachine) => RegistryKey.OpenBaseKey(hive, bit);
    }

    public class VcRedistInfo
    {
        private static readonly VcRedistInfo Vs2012 = new VcRedistInfo(VCRedists.VS2012,
            "http://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_x86.exe",
            "msvcr110.dll");

        private static readonly VcRedistInfo Vs2013 = new VcRedistInfo(VCRedists.VS2013,
            "http://download.microsoft.com/download/2/E/6/2E61CFA4-993B-4DD4-91DA-3737CD5CD6E3/vcredist_x86.exe",
            "msvcr120.dll");

        // Update 2
        private static readonly VcRedistInfo Vs2015 = new VcRedistInfo(VCRedists.VS2015,
            "https://download.microsoft.com/download/0/5/0/0504B211-6090-48B1-8DEE-3FF879C29968/vc_redist.x86.exe",
            "vcruntime140.dll");

        private VcRedistInfo(VCRedists version, string url, string systemFile)
        {
            Version = version;
            Url = url;
            SystemFile = systemFile;
        }

        public VCRedists Version { get; }
        public string Url { get; }
        public string SystemFile { get; }

        public static VcRedistInfo GetInfo(VCRedists v)
        {
            switch (v)
            {
                case VCRedists.VS2012:
                {
                    return Vs2012;
                }
                case VCRedists.VS2013:
                {
                    return Vs2013;
                }
                case VCRedists.VS2015:
                {
                    return Vs2015;
                }
                default:
                {
                    throw new NotSupportedException();
                }
            }
        }
    }
}