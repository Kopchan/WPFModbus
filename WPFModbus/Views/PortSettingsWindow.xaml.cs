using System.Windows;
using System.IO.Ports;
using WPFModbus.Helpers;
using WPFModbus.Models;

namespace WPFModbus.Views
{
    /// <summary>
    /// Логика взаимодействия для PortSettingsWindow.xaml
    /// </summary>
    public partial class PortSettingsWindow : Window
    {
        private List<Port> Ports = [];

        public PortSettingsWindow()
        {
            InitializeComponent();

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
                Ports.Add(new Port(portName, isAvaliable));
            }

             Timeout_CB.Text          = Properties.Settings.Default.PortTimeout.ToString();
            Baudrate_CB.Text          = Properties.Settings.Default.PortBaudrate.ToString();
            DataBits_CB.SelectedItem  = Properties.Settings.Default.PortDataBits;
            StopBits_CB.SelectedItem  = Properties.Settings.Default.PortStopBits;
              Parity_CB.SelectedItem  = Properties.Settings.Default.PortParity;
              Parity_CB.ItemsSource   = Models.Parity.Types;
               Ports_CB.SelectedValue = Properties.Settings.Default.PortName;
               Ports_CB.ItemsSource   = Ports;

            if (Properties.Settings.Default.PortName == "")
                Ports_CB.SelectedIndex = 0;
        }

        private void Save(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.PortName     =            Ports_CB.SelectedItem.ToString();
            Properties.Settings.Default.PortParity   =           Parity_CB.SelectedItem.ToString();
            Properties.Settings.Default.PortBaudrate = Convert.ToInt32(Baudrate_CB.Text);
            Properties.Settings.Default.PortTimeout  = Convert.ToInt32(Timeout_CB.Text);
            Properties.Settings.Default.PortDataBits =    (int)DataBits_CB.SelectedItem;
            Properties.Settings.Default.PortStopBits = (double)StopBits_CB.SelectedItem;
            Properties.Settings.Default.Save();
            DialogResult = true;
            Close();
        }

        protected override void OnSourceInitialized(EventArgs e) => IconHelper.RemoveIcon(this);
    }
}
