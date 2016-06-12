using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace VCRedistInstaller
{
    public class Handler
    {
        private readonly VcRedistInstaller _installer;

        public Handler(VcRedistInstaller installer)
        {
            _installer = installer;
        }

        public async Task HandleAll()
        {
            try
            {
                await HandleInternal();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Trouble! " + ex);
                Environment.Exit(1);
            }
        }

        private async Task HandleInternal()
        {
            const string elevatedOpt = "--elevated";
            const string forceOpt = "--force";
            var commandLineArgs = Environment.GetCommandLineArgs();
            var cli = Environment.GetCommandLineArgs().Skip(1);
            var elevated = cli.Contains(elevatedOpt);
            var forced = cli.Contains(forceOpt);
            var versionsCli = cli.Where(x => !x.StartsWith("--")).ToArray();
            var versions =
                versionsCli.Select(x => (VCRedists) Enum.Parse(typeof (VCRedists), x))
                    .Select(VcRedistInfo.GetInfo)
                    .ToArray();
            var toBeInstalled = forced ? versions : await _installer.Checker(versions);
            if (!toBeInstalled.Any())
            {
                Environment.Exit(0);
                return;
            }

            if (elevated)
            {
                await _installer.DownloadAndInstall(toBeInstalled);
            }
            else
            {
                using (
                    var p =
                        Process.Start(new ProcessStartInfo(commandLineArgs[0],
                            string.Join(" ", new[] {elevatedOpt, forced ? forceOpt : null}.Where(x => x != null).Concat(versionsCli)))
                        {
                            Verb = "runas"
                        })) p.WaitForExit();
            }
            Environment.Exit(0);
        }
    }
}
