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
            var commandLineArgs = Environment.GetCommandLineArgs();
            const string value = "--elevated";
            var elevated = commandLineArgs.Contains(value);
            var cli = commandLineArgs.Skip(1).Where(x => x != value).ToArray();
            var versions = cli.Select(x => (VCRedists) Enum.Parse(typeof (VCRedists), x)).ToArray();
            var toBeInstalled = await _installer.Checker(versions);
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
                            string.Join(" ", new[] {value}.Concat(cli)))
                        {
                            Verb = "runas"
                        })) p.WaitForExit();
            }
            Environment.Exit(0);
        }
    }
}
