using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

    // http://stackoverflow.com/questions/12206314/detect-if-visual-c-redistributable-for-visual-studio-2012-is-installed
    // http://stackoverflow.com/questions/21702199/how-to-determine-if-the-32-bit-visual-studio-2013-redistributable-is-installed-o
    // https://social.msdn.microsoft.com/Forums/sqlserver/en-US/c599b7e9-ee1a-4491-8ae4-523ffcf201c2/how-to-detect-the-msvc-runtime-2013-installed-on-system-or-not?forum=vssetup
    // https://community.flexerasoftware.com/showthread.php?220517-Handling-Detection-of-Visual-C-2015-x86-Runtime-Without-Checking-Product
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
                    var installErrors = new List<Exception>();
                    foreach (var c in bc.GetConsumingEnumerable())
                    {
                        try
                        {
                            await Install(c);
                        }
                        catch (Exception ex)
                        {
                            installErrors.Add(ex);
                        }
                    }
                    if (installErrors.Any())
                        throw new AggregateException("An error occurred during Installing", installErrors);
                }).Unwrap();
                var downloadErrors = new List<Exception>();
                foreach (var v in versions)
                {
                    try
                    {
                        var fileName = Path.Combine(Path.GetTempPath(), $"vcredist_x86-{v.Version}.exe");
                        await DownloadVersion(fileName, v.Url);
                        bc.Add(fileName);
                    }
                    catch (Exception ex)
                    {
                        downloadErrors.Add(ex);
                    }
                }
                bc.CompleteAdding();
                await t;
                if (downloadErrors.Any())
                    throw new AggregateException("An error occurred during downloading", downloadErrors);
            }
        }

        private Task Install(string fileName) => Task.Factory.StartNew(() =>
        {
            // TODO: Error handling
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

        private static bool VerifyRegistry(VcRedistInfo info, RegistryView bit) => VerifyDevDiv(info, bit) && VerifyVS(info, bit);

        private static bool VerifyVS(VcRedistInfo info, RegistryView bit)
        {
            var regVersion = $"{(int)info.Version}.0";
            using (var reg = OpenRegistry(RegistryView.Registry32) // must look up in 32-bit registry
                .OpenSubKey($@"SOFTWARE\Microsoft\VisualStudio\{regVersion}\VC\Runtimes\" +
                            (bit == RegistryView.Registry32 ? "x86" : "x64")))
            {
                if (reg != null && TryConfirm(reg, "Installed"))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool VerifyDevDiv(VcRedistInfo info, RegistryView bit)
        {
            var regVersion = $"{(int) info.Version}.0";
            using (var reg = OpenRegistry(bit)
                .OpenSubKey($@"SOFTWARE\Microsoft\DevDiv\vc\Servicing\{regVersion}\RuntimeMinimum"))
            {
                if (reg != null && TryConfirm(reg, "Install"))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TryConfirm(RegistryKey reg, string parameter)
        {
            try
            {
                return Convert.ToInt64(reg.GetValue(parameter)) == 1;
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
        // Update 4
        private static readonly VcRedistInfo Vs2012 = new VcRedistInfo(VCRedists.VS2012,
            "http://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_x86.exe",
            "msvcr110.dll");

        // Update 5
        private static readonly VcRedistInfo Vs2013 = new VcRedistInfo(VCRedists.VS2013,
            "http://download.microsoft.com/download/C/C/2/CC2DF5F8-4454-44B4-802D-5EA68D086676/vcredist_x86.exe",
            "msvcr120.dll");

        // Update 3
        private static readonly VcRedistInfo Vs2015 = new VcRedistInfo(VCRedists.VS2015,
            "http://download.microsoft.com/download/6/D/F/6DF3FF94-F7F9-4F0B-838C-A328D1A7D0EE/vc_redist.x86.exe",
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