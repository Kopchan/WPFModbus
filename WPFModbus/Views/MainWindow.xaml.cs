using System.Windows;
using System.IO.Ports;
using WPFModbus.ViewModels;
using WPFModbus.Models;
using System.Windows.Media;
using System.Linq;
using System;
using System.Text;
using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Shapes;

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
            try
            {
                //port.DtrEnable = true;
                //port.RtsEnable = true;
                ViewModel.Port = port;
                port.Open();
                StartRead();
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "Для работы с программый необходимо выбрать не закрытый порт",
                    "Выберите порт",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        // Обработка входящих данных
        private void StartRead()
        {
            Task.Run(() =>
            {
                //Thread.Sleep(1000);
                ulong i = 0;
                while (port.IsOpen)
                {
                    Thread.Sleep(100);
                    int bytes = port.BytesToRead;
                    if (bytes < 1) continue;

                    byte[] buffer = new byte[bytes];
                    port.Read(buffer, 0, bytes);
                    ReceivedLine line = new(i, DateTime.Now, buffer);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ViewModel.ReceivedLines.Add(line);
                        Output_DG.ScrollIntoView(line);
                    });
                    i++;
                }
                MessageBox.Show("DEV: end read task");
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
            else
            {
                try
                {
                    port.Open();
                }
                catch (Exception)
                {
                    MessageBox.Show(
                        "Для работы с программый необходимо выбрать не закрытый порт",
                        "Выберите порт",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
            }

            ViewModel.ReceivedLines.Add(new ReceivedLine(28, DateTime.Now, ASCIIEncoding.ASCII.GetBytes("DEV: Test add on setup")));
        }

        private void Exit_MI_Click(object sender, RoutedEventArgs e) => Close();

        private void ClearOutput(object sender, RoutedEventArgs e) => ViewModel.ReceivedLines.Clear();
    }
}