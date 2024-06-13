using System.Windows;
using System.IO.Ports;
using WPFModbus.Helpers;
using WPFModbus.Models;
using System.Linq;
using System.Windows.Shapes;
using System.ComponentModel;

namespace WPFModbus.Views
{
    /// <summary>
    /// Логика взаимодействия для PortSettingsWindow.xaml
    /// </summary>
    public partial class PortSettingsWindow : Window, INotifyPropertyChanged
    {
        private bool isScan = false;
        public bool IsScan { 
            get => isScan; 
            set 
            {
                isScan = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsScan)));
            } 
        }

        public PortSettingsWindow()
        {
            DataContext = this;
            InitializeComponent();
            GetPorts();

             Timeout_CB.Text          = Properties.Settings.Default.PortTimeout .ToString();
            Baudrate_CB.Text          = Properties.Settings.Default.PortBaudrate.ToString();
            DataBits_CB.SelectedItem  = Properties.Settings.Default.PortDataBits;
            StopBits_CB.SelectedItem  = Properties.Settings.Default.PortStopBits;
              Parity_CB.ItemsSource   = Models.Parity.Types;
              Parity_CB.SelectedItem  = Models.Parity.Types.FirstOrDefault(t => t.Code == Properties.Settings.Default.PortParity);

            if (Properties.Settings.Default.PortName == "")
                Ports_CB.SelectedIndex = 0;
        }

        private void Save(object sender, RoutedEventArgs e)
        {
            try
            {
                try { Properties.Settings.Default.PortName = Ports_CB?.SelectedItem?.ToString() ?? Ports_CB?.Items[0]?.ToString() ?? ""; }
                catch { throw new Exception("Необходимен COM порт для продолжения"); }
                Properties.Settings.Default.PortParity = ((Models.Parity)Parity_CB.SelectedItem).Code;
                Properties.Settings.Default.PortBaudrate = Int32.TryParse(Baudrate_CB.Text, out int baudrate) ? baudrate : throw new Exception("Введите число в качестве скорости");
                Properties.Settings.Default.PortTimeout  = Int32.TryParse( Timeout_CB.Text, out int timeout ) ? timeout  : throw new Exception("Введите число в качестве таймаута");
                Properties.Settings.Default.PortDataBits =    (int)DataBits_CB.SelectedItem;
                Properties.Settings.Default.PortStopBits = (double)StopBits_CB.SelectedItem;
                Properties.Settings.Default.Save();
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged(PropertyChangedEventArgs e) =>
            PropertyChanged?.Invoke(this, e);

        protected override void OnSourceInitialized(EventArgs e) => 
            IconHelper.RemoveIcon(this);

        private void RefreshPorts(object sender, RoutedEventArgs e) =>
            GetPorts();

        private void GetPorts()
        {
            Task.Run(() => 
            {
                IsScan = true;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Ports_CB.Items.Clear();
                });
                foreach (string portName in SerialPort.GetPortNames())
                {
                    bool isAvaliable = false;
                    SerialPort port = new(portName);
                    try
                    {
                        port.Open();
                        isAvaliable = true;
                    }
                    catch (Exception) { }

                    port.Close();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Ports_CB.Items.Add(new Port(portName, isAvaliable));

                        Ports_CB.SelectedValue = Properties.Settings.Default.PortName;

                        if (Ports_CB.SelectedItem == null)
                            Ports_CB.SelectedIndex = 0;
                    });
                }
                IsScan = false;
            });
        }
    }
}
