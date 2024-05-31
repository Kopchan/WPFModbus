using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Text;
using WPFModbus.Models;

namespace WPFModbus.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty] public string errorMessage = "";
        [ObservableProperty] public SerialPort? port;
        [ObservableProperty] public bool portIsOpen = false;
        [ObservableProperty] public bool isSending = false;
        [ObservableProperty] public bool inBottomDG = true;
        [ObservableProperty] public ObservableCollection<ReceivedLine> receivedLines = [];
    }
}
