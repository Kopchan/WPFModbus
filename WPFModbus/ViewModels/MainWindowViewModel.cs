using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Text;
using WPFModbus.Models;

namespace WPFModbus.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty] public string sendBTContent = "Отправить";
        [ObservableProperty] public SerialPort? port;
        public ObservableCollection<ReceivedLine> ReceivedLines { get; } =
        [
            new ReceivedLine(1, DateTime.Now, ASCIIEncoding.ASCII.GetBytes("DEV: Test add on init")),
            new ReceivedLine(2, DateTime.Now, ASCIIEncoding.ASCII.GetBytes("DEV: Test add on init 2")),
        ];
    }
}
