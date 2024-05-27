using System.Windows;
using System.IO.Ports;
using WPFModbus.ViewModels;
using WPFModbus.Models;
using System.Windows.Media;
using System.Linq;
using System;

namespace WPFModbus.Views
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SerialPort port;
        CancellationTokenSource cancelRead = new();
        private bool isSending = false;

        MainWindowViewModel ViewModel { get; } = new();

        public MainWindow()
        {
            DataContext = ViewModel;
            InitializeComponent();

            // Проверка установленного до этого порта
            try
            {
                SerialPort tryPort = new(Properties.Settings.Default.PortName);
                tryPort.Open();
                tryPort.Close();
            }
            catch
            {
                var window = new PortSettingsWindow();
                window.ShowDialog();
            }
            Init();
        }

        // Подключение к порту
        private void Init()
        {
            port = new()
            {
                PortName = Properties.Settings.Default.PortName,
                BaudRate = Properties.Settings.Default.PortBaudrate,
                DataBits = Properties.Settings.Default.PortDataBits,
                StopBits = Properties.Settings.Default.PortStopBits switch
                {
                    2   => StopBits.Two,
                    1.5 => StopBits.OnePointFive,
                    _   => StopBits.One,
                },
                Parity = Properties.Settings.Default.PortParity switch
                {
                    "odd"   => System.IO.Ports.Parity.Odd,
                    "even"  => System.IO.Ports.Parity.Even,
                    "mark"  => System.IO.Ports.Parity.Mark,
                    "space" => System.IO.Ports.Parity.Space,
                    _       => System.IO.Ports.Parity.None,
                },
                WriteTimeout = Properties.Settings.Default.PortTimeout,
            };
            port.DtrEnable = true;
            port.RtsEnable = true;
            ViewModel.Port = port;
            port.Open();
            StartRead();
        }

        // Обработка входящих данных
        private void StartRead()
        {
            Task.Run(() =>
            {
                ulong i = 0;
                while (port.IsOpen == true)
                {
                    int bytes = port.BytesToRead;
                    MessageBox.Show(bytes.ToString());

                    byte[] buffer = new byte[bytes];
                    port.Read(buffer, 0, bytes);

                    //MessageBox.Show(String.Join(' ', buffer));
                    ViewModel.ReceivedLines.Add(new ReceivedLine(i, DateTime.Now, buffer));
                    i++;
                }
            }, cancelRead.Token);
        }

        // Обработка исходящих данных
        private void SendMessage(object sender, RoutedEventArgs e)
        {
            // Отмена отправки, если она происходила
            if (isSending)
            {
                port.DiscardOutBuffer();
                isSending = false;
                ViewModel.SendBTContent = "Отправить";
                return;
            }

            // Установка состояние отправки 
            isSending = true;
            ViewModel.SendBTContent = "Отмена";
            var input = Input_TBx.Text;

            Task.Run(() =>
            {
                try
                {
                    // Отправка
                    port.WriteLine(input);
                }
                catch (Exception ex)
                {
                    // Таймаут или другие ошибки
                    if (isSending) MessageBox.Show(
                        "Ошибка при отправке: " + (ex is TimeoutException ? "Время вышло" : ex.Message),
                        "Ошибка отправки",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
                finally
                {
                    // Переключение статуса
                    isSending = false;
                    ViewModel.SendBTContent = "Отправить";
                }
            });
        }

        // Открытие настроек, отключаясь от порта
        private void OpenPortSettings(object sender, RoutedEventArgs e)
        {
            port.Close();
            PortSettingsWindow window = new();

            bool isChanged = window.ShowDialog() ?? true;
            if (isChanged) Init();
            else port.Open();
            ViewModel.ReceivedLines.Add(new ReceivedLine(28, DateTime.Now, [34, 43, 54]));
        }

        private void Exit_MI_Click(object sender, RoutedEventArgs e) => Close();

        private void ClearOutput(object sender, RoutedEventArgs e) => ViewModel.ReceivedLines.Clear();
    }
}