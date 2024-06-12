using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Text;
using WPFModbus.Models;
using WPFModbus.Enums;

namespace WPFModbus.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [NotifyPropertyChangedFor("SelectedEncodingName")]
        [ObservableProperty] public Encoding selectedEncoding = Encoding.ASCII;
        [ObservableProperty] public ObservableCollection<ReceivedLine> receivedLines = [];
        [ObservableProperty] public ObservableCollection<EncodingInfo> encodings = [];
        [ObservableProperty] public string errorMessage = "";
        [ObservableProperty] public SerialPort? port;
        [ObservableProperty] public bool portIsOpen = false;
        [ObservableProperty] public bool isSending = false;
        [ObservableProperty] public bool isSendingInterval = false;
        [ObservableProperty] public bool inBottomDG = true;

        [ObservableProperty] public SendDataType   sendDataType   = SendDataType.HEX;
        [ObservableProperty] public SendMode       sendMode       = SendMode.RAW;
        [ObservableProperty] public SendMBProtocol sendMBProtocol = SendMBProtocol.RTU;
        [ObservableProperty] public SendMBFunc     sendMBFunc     = SendMBFunc.ReadCoilStatus;
        [ObservableProperty] public bool  sendIsInterval = false;
        [ObservableProperty] public ulong sendInterval = 1000;

        public string SelectedEncodingName => SelectedEncoding.CodePage == 20127 
            ? "ASCII" 
            : Encodings?.FirstOrDefault(e => 
                e.CodePage == SelectedEncoding.CodePage
            )?.Name ?? "ASCII";
    }
}
