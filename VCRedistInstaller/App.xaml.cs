using System.Threading.Tasks;
using System.Windows;

namespace VCRedistInstaller
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            Startup += (sender, args) => Task.Factory.StartNew(() => new Handler(new VcRedistInstaller()).HandleAll());
        }
    }
}