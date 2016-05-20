using System;
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

    public class VcRedistInstaller
    {
        public async Task<VCRedists[]> Checker(params VCRedists[] versions)
            => versions.Where(v => !CheckVcRedistInstalled($"{(int)v}.0")).ToArray();

        public async Task DownloadAndInstall(params VCRedists[] versions)
        {
            var tasks = new List<Task>();
            foreach (var v in versions)
            {
                var fileName = Path.Combine(Path.GetTempPath(), $"vcredist_x86-{v}.exe");
                await DownloadVersion(fileName, GetUrl(v));
                tasks.Add(Install(fileName));
            }
            await Task.WhenAll(tasks);
        }

        private Task Install(string fileName) => Task.Factory.StartNew(() =>
        {
            using (var p = Process.Start(fileName, "/q /norestart"))
                p.WaitForExit();
        });

        private async Task DownloadVersion(string fileName, string url)
        {
            using (var webClient = new WebClient())
            {
                await webClient.DownloadFileTaskAsync(url, fileName);
            }
        }

        private static string GetUrl(VCRedists v)
        {
            switch (v)
            {
                case VCRedists.VS2012:
                {
                    return
                        "http://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_x86.exe";
                }
                case VCRedists.VS2013:
                {
                    return
                        "http://download.microsoft.com/download/2/E/6/2E61CFA4-993B-4DD4-91DA-3737CD5CD6E3/vcredist_x86.exe";
                }
                case VCRedists.VS2015:
                {
                        // Update 2
                    return
                        "https://download.microsoft.com/download/0/5/0/0504B211-6090-48B1-8DEE-3FF879C29968/vc_redist.x86.exe";
                }
                default:
                {
                    throw new NotSupportedException();
                }
            }
        }

        private static bool CheckVcRedistInstalled(string regVersion, RegistryView bit = RegistryView.Registry32)
        {
            using (var reg = OpenRegistry(bit)
                .OpenSubKey($@"SOFTWARE\Microsoft\DevDiv\vc\Servicing\{regVersion}\RuntimeMinimum"))
            {
                if (reg != null)
                    return true;
            }
            return false;
        }

        static RegistryKey OpenRegistry(RegistryView bit = RegistryView.Registry32,
            RegistryHive hive = RegistryHive.LocalMachine) => RegistryKey.OpenBaseKey(hive, bit);
    }
}