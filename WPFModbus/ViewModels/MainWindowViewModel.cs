using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.IO.Ports;
using WPFModbus.Models;

namespace WPFModbus.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty] public string       sendBTContent = "Отправить";
        [ObservableProperty] public ObservableCollection<ReceivedLine> receivedLines = 
        [
            new ReceivedLine(27, DateTime.Now, [102, 45, 20]),
            new ReceivedLine(27, DateTime.Now, [102, 45, 20]),
            new ReceivedLine(27, DateTime.Now, [102, 45, 20]),
        ];
        [ObservableProperty] public SerialPort? port;
    }
}
