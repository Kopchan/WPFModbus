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
using WPFModbus.Enums;
using WPFModbus.Helpers;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;
using NModbus.Device;
using System.Diagnostics.Metrics;
using NModbus;
using NModbus.Serial;

namespace WPFModbus.Views
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SerialPort port;
        private System.Timers.Timer sendTimer = new();
        private byte[] sendBuffer = [];
        private string input = "";

        private MainWindowViewModel ViewModel { get; } = new();

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
                PortSettingsWindow window = new();
                window.ShowDialog();
            }
            Init();
            GetEncodings();

            sendTimer.Elapsed += OnSendTimerEvent;
        }

        // Подключение к порту
        private void Init()
        {
            try
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
                    ReadTimeout  = Properties.Settings.Default.PortTimeout,
                };
                ViewModel.Port = port;
                port.Open();
                StartRead();
            }
            catch
            {
                MessageBox.Show(
                    "Для работы с программой необходимо выбрать не закрытый порт",
                    "Выберите порт",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        // Получение кодировок
        private void GetEncodings()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            foreach (var enc in Encoding.GetEncodings())
                ViewModel.Encodings.Add(enc);

            var groupedEncodings = ViewModel.Encodings
                .Select(info => new { EncodingInfo = info, GroupName = GetGroupName(info.DisplayName) })
                .GroupBy(x => x.GroupName);

            foreach (var group in groupedEncodings)
            {
                MenuItem groupMenuItem = new() 
                { 
                    Header = group.Key 
                };
                foreach (var item in group)
                {
                    MenuItem encodingMenuItem = new()
                    {
                        Header = item.EncodingInfo.DisplayName,
                        Tag    = item.EncodingInfo.CodePage
                    };
                    encodingMenuItem.Click += Encoding_MI_Click;
                    groupMenuItem.Items.Add(encodingMenuItem);
                }
                Encodings_MI.Items.Add(groupMenuItem);
            }
        }

        // Извлечение основного названия кодировки
        private string GetGroupName(string displayName)
        {
            var match = Regex.Match(displayName, @"^(.+)\s+\(.+\)$");
            return match.Success ? match.Groups[1].Value : displayName;
        }

        // Обработка входящих данных
        private void StartRead()
        {
            Task.Run(() =>
            {
                // Остановка задачи чтения, если порт вдруг закрыт
                while (ViewModel.PortIsOpen = port.IsOpen)
                {
                    if (ViewModel.SendMode == SendMode.Modbus) break;

                    // Усыпление задачи, если нет байтов для чтения
                    int bytes = port.BytesToRead;
                    if (bytes < 1) 
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    // Чтение полученных данных
                    byte[] buffer = new byte[bytes];
                    port.Read(buffer, 0, bytes);
                    ReceivedLine line = new()
                    {
                        Id   = (Convert.ToUInt64(ViewModel.ReceivedLines?.LastOrDefault()?.Id ?? "0") + 1).ToString("D5"),
                        Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                        Data = BitConverter.ToString(buffer).Replace('-', ' '),
                        Text = SanitizeString.ReplaceControl(ViewModel.SelectedEncoding.GetString(buffer))
                    };

                    // Запись данных в таблицу в основном потоке
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ViewModel.ReceivedLines.Add(line);
                        if (ViewModel.InBottomDG) Output_DG.ScrollIntoView(line);
                    });
                }
            });
        }

        // Обработка исходящих данных
        private void StartStopSend(object sender, RoutedEventArgs e)
        {
            // Очистка ошибки
            ViewModel.ErrorMessage = "";

            input = Input_TBx.Text;

            // Отмена отправки, если она происходила
            if (ViewModel.IsSending || ViewModel.IsSendingInterval)
            {
                port.DiscardOutBuffer();
                ViewModel.IsSending = ViewModel.IsSendingInterval = false;
                sendTimer.Enabled = false;
                return;
            }

            if (ViewModel.SendMode == SendMode.RAW)
            {
                // Перевод строки ввода в байты 
                sendBuffer = ViewModel.SendDataType switch
                {
                    SendDataType.Text => Encoding.GetEncoding(ViewModel.SelectedEncoding?.CodePage ?? 20127).GetBytes(input),
                    _ => HexStringToByteArray.Convert(input)
                };

                SendRAW();

                if (ViewModel.SendIsInterval)
                {
                    ViewModel.IsSendingInterval = true;
                    sendTimer.Interval = ViewModel.SendInterval;
                    sendTimer.Enabled = true;
                }
                return;
            }
            else
            {
                SendModbus();

                if (ViewModel.SendIsInterval)
                {
                    ViewModel.IsSendingInterval = true;
                    sendTimer.Interval = ViewModel.SendInterval;
                    sendTimer.Enabled = true;
                }
                return;
            }
        }

        private void OnSendTimerEvent(object? source, System.Timers.ElapsedEventArgs e) 
        {
            if (ViewModel.SendMode == SendMode.RAW) 
                SendRAW();
            else 
                SendModbus();
        }

        private void SendRAW()
        {
            Task.Run(() =>
            {
                try
                {
                    // Установка состояние отправки 
                    ViewModel.IsSending = true;

                    if (!port.IsOpen)
                    {
                        port.Open();
                        StartRead();
                    }

                    // Отправка
                    port.Write(sendBuffer, 0, sendBuffer.Length);

                    // Очистка ошибки
                    ViewModel.ErrorMessage = "";
                }
                catch (Exception ex)
                {
                    // Таймаут или другие ошибки
                    if (ViewModel.IsSending) ViewModel.ErrorMessage =
                        "Ошибка при отправке: " + ex switch
                        {
                            TimeoutException => "Время ожидания вышло",
                            UnauthorizedAccessException => "Порт недоступен/закрыт",
                            FileNotFoundException => "Порт не найден",
                            _ => ex.Message
                        };
                }
                finally
                {
                    // Переключение статуса
                    ViewModel.IsSending = false;
                }
            });
        }

        private void SendModbus()
        {
            Task.Run(() =>
            {
                try
                {
                    // Установка состояние отправки 
                    ViewModel.IsSending = true;

                    if (!port.IsOpen)
                    {
                        port.Open();
                        StartRead();
                    }

                    SerialPortAdapter adapter = new(port);
                    ModbusFactory factory = new();
                    IModbusMaster master = ViewModel.SendMBProtocol switch
                    {
                        SendMBProtocol.RTU => factory.CreateRtuMaster  (adapter),
                        _                  => factory.CreateAsciiMaster(adapter),
                    };

            
                    bool  [] bools   = [];
                    ushort[] ushorts = [];

                    List<bool> inputBools   = [];
                    ushort[]   inputUshorts = [];

                    if (ViewModel.SendMBFunc == SendMBFunc.WriteCoils)
                        for (var i = 0; i < input.Length; i++)
                            inputBools.Add(Convert.ToInt16(input[i].ToString()) == 1);

                    if (ViewModel.SendMBFunc == SendMBFunc.WriteRegisters) 
                    {
                        input = Regex.Replace(
                            input.Replace("0x", ""),
                            "[^0-9A-Fa-f]",
                            ""
                        );
                        while (input.Length % 4 != 0) input = input.Insert(0, "0");
                        inputUshorts = Enumerable.Range(0, input.Length)
                             .Where(x => x % 4 == 0)
                             .Select(x => System.Convert.ToUInt16(input.Substring(x, 4), 16))
                             .ToArray();
                    }

                    switch (ViewModel.SendMBFunc)
                    {
                        case SendMBFunc.ReadCoils           : bools   = master.ReadCoils             (ViewModel.SlaveId, ViewModel.StartAddress, ViewModel.Quantity  ); break;
                        case SendMBFunc.ReadInputs          : bools   = master.ReadInputs            (ViewModel.SlaveId, ViewModel.StartAddress, ViewModel.Quantity  ); break;
                        case SendMBFunc.ReadHoldingRegisters: ushorts = master.ReadHoldingRegisters  (ViewModel.SlaveId, ViewModel.StartAddress, ViewModel.Quantity  ); break;
                        case SendMBFunc.ReadInputRegisters  : ushorts = master.ReadInputRegisters    (ViewModel.SlaveId, ViewModel.StartAddress, ViewModel.Quantity  ); break;
                        case SendMBFunc.WriteCoils          :           master.WriteMultipleCoils    (ViewModel.SlaveId, ViewModel.StartAddress, inputBools.ToArray()); break;
                        case SendMBFunc.WriteRegisters      :           master.WriteMultipleRegisters(ViewModel.SlaveId, ViewModel.StartAddress, inputUshorts        ); break;
                    }

                    string output = "";
                    if (bools.Length > 0) output = String.Join("", bools);
                    else
                        foreach(var num in ushorts)
                            output += num.ToString("X4") + " ";


                    ReceivedLine line = new()
                    {
                        Id   = (Convert.ToUInt64(ViewModel.ReceivedLines?.LastOrDefault()?.Id ?? "0") + 1).ToString("D5"),
                        Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                        Data = output,
                    };
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ViewModel.ReceivedLines.Add(line);
                        if (ViewModel.InBottomDG) Output_DG.ScrollIntoView(line);
                    });

                    // Очистка ошибки
                    ViewModel.ErrorMessage = "";
            }
                catch (Exception ex)
                {
                // Таймаут или другие ошибки
                if (ViewModel.IsSending) ViewModel.ErrorMessage =
                    "Ошибка при отправке: " + ex switch
                    {
                        TimeoutException => "Время ожидания вышло. Возможно данного слейва не существует",
                        UnauthorizedAccessException => "Порт недоступен/закрыт",
                        FileNotFoundException => "Порт не найден",
                        SlaveException => "Вы уверены что этот адрес и этот слейв принимает данную функцию?",
                        IOException => "Ошибка проверки хеш-суммы. Вы уверены, что это на этом COM-порту Modbus устройство?",
                        _ => ex.Message
                    };
            }
                finally
                {
                // Переключение статуса
                ViewModel.IsSending = false;
            }
        });
        }

        // Открытие настроек, отключаясь от порта
        private void OpenPortSettings(object sender, RoutedEventArgs e)
        {
            ViewModel.IsSendingInterval = false;
            sendTimer.Enabled = false;

            // Закрытие порта (остановка чтения)
            port.Close();

            // Создание окна в центре родителя
            PortSettingsWindow window = new() { Owner = this };

            // Переинициализация подключение к порту, если настройки изменили (сохранили)
            bool isChanged = window.ShowDialog() ?? true;
            if (isChanged)
            {
                Init();
                return;
            }
            // Иначе попытка открыть старое соединение
            try
            {
                port.Open();
                StartRead();
            }
            catch
            {
                MessageBox.Show(
                    "Для работы с программой необходимо выбрать не закрытый порт",
                    "Выберите порт",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        // Обработка скролла для статуса "внизу таблицы" (для отключения автоскролла до новых записей)
        private void Output_DG_ScrollChanged(object sender, ScrollChangedEventArgs e) =>
            ViewModel.InBottomDG = e.VerticalOffset == e.ExtentHeight - e.ViewportHeight;

        // Закрытие через меню
        private void Exit_MI_Click(object sender, RoutedEventArgs e) => 
            Close();
        
        private void Encoding_MI_Click(object sender, RoutedEventArgs e) => 
            ViewModel.SelectedEncoding = Encoding.GetEncoding((int)((MenuItem)sender).Tag);

        // Очистка таблицы
        private void ClearOutput(object sender, RoutedEventArgs e) => 
            ViewModel.ReceivedLines.Clear();

        // Очистка ошибки при вводе
        private void Input_TBx_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (InputGridRow.ActualHeight > Math.Min(InputSide.Height, Height - 200))
                InputGridRow.Height = GridLength.Auto;

            ViewModel.ErrorMessage = "";
        }

        private void RecalcInputMaxHeight(object sender, SizeChangedEventArgs e)
        {
            var maxHeight = InputGridRow.MaxHeight = 
                Math.Min(InputSide.ActualHeight, Height - 200);

            if (InputGridRow.ActualHeight > maxHeight)
                InputGridRow.Height = GridLength.Auto;
        }

        private void CheckMaxSize(object sender, MouseButtonEventArgs e)
        {
            if (InputGridRow.ActualHeight == Math.Min(InputSide.ActualHeight, Height - 200))
                InputGridRow.Height = GridLength.Auto;
        }

        // Запись размеров и положения окна, поля ввода и режимов во время закрытия для последущего восстановление
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            { 
                // Берём прошлые размеры, если окно в полноэкранном режиме
                Properties.Settings.Default.Top       = RestoreBounds.Top;
                Properties.Settings.Default.Left      = RestoreBounds.Left;
                Properties.Settings.Default.Height    = RestoreBounds.Height;
                Properties.Settings.Default.Width     = RestoreBounds.Width;
                Properties.Settings.Default.Maximized = true;
            }
            else
            {
                Properties.Settings.Default.Top       = Top;
                Properties.Settings.Default.Left      = Left;
                Properties.Settings.Default.Height    = Height;
                Properties.Settings.Default.Width     = Width;
                Properties.Settings.Default.Maximized = false;
            }

            Properties.Settings.Default.Input          = Input_TBx.Text;
            Properties.Settings.Default.SendDataType   = ViewModel.SendDataType  .ToString();
            Properties.Settings.Default.SendMode       = ViewModel.SendMode      .ToString();
            Properties.Settings.Default.SendMBProtocol = ViewModel.SendMBProtocol.ToString();
            Properties.Settings.Default.SendMBFunc     = ViewModel.SendMBFunc    .ToString();
            Properties.Settings.Default.SendIsInterval = ViewModel.SendIsInterval;
            Properties.Settings.Default.SendInterval   = ViewModel.SendInterval;
            Properties.Settings.Default.SlaveId        = ViewModel.SlaveId;
            Properties.Settings.Default.StartAddress   = ViewModel.StartAddress;
            Properties.Settings.Default.Quantity       = ViewModel.Quantity;

            Properties.Settings.Default.EncodingCodePage = ViewModel.SelectedEncoding?.CodePage ?? 0;

            Properties.Settings.Default.Save();
        }

        // Восстановление размеров и положения окна, поля ввода и режимов
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            try
            {
                if (Properties.Settings.Default.Height > 0)
                {
                    // Если размеры в настройках не по умолчанию — восстанавливаем
                    Top    = Properties.Settings.Default.Top;
                    Left   = Properties.Settings.Default.Left;
                    Height = Properties.Settings.Default.Height;
                    Width  = Properties.Settings.Default.Width;
                }

                if (Properties.Settings.Default.Maximized)
                    WindowState = WindowState.Maximized;

                Input_TBx.Text = Properties.Settings.Default.Input;
                ViewModel.SendDataType = (SendDataType)Enum.Parse(
                    typeof(SendDataType), 
                    Properties.Settings.Default.SendDataType
                );
                ViewModel.SendMode = (SendMode)Enum.Parse(
                    typeof(SendMode), 
                    Properties.Settings.Default.SendMode
                );
                ViewModel.SendMBProtocol = (SendMBProtocol)Enum.Parse(
                    typeof(SendMBProtocol), 
                    Properties.Settings.Default.SendMBProtocol
                );
                ViewModel.SendMBFunc = (SendMBFunc)Enum.Parse(
                    typeof(SendMBFunc), 
                    Properties.Settings.Default.SendMBFunc
                );

                ViewModel.SendIsInterval = Properties.Settings.Default.SendIsInterval;
                ViewModel.SendInterval   = Properties.Settings.Default.SendInterval;
                ViewModel.SlaveId        = Properties.Settings.Default.SlaveId;
                ViewModel.StartAddress   = Properties.Settings.Default.StartAddress;
                ViewModel.Quantity       = Properties.Settings.Default.Quantity;

                InputGridRow.MaxHeight = Math.Min(InputSide.ActualHeight, Height - 200);

                if (Properties.Settings.Default.EncodingCodePage > 0)
                    ViewModel.SelectedEncoding = ViewModel.Encodings?.FirstOrDefault(e =>
                        e.CodePage == Properties.Settings.Default.EncodingCodePage
                    )?.GetEncoding() ?? Encoding.ASCII;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERR on init settings: " + ex.Message);
            }
        }

        // Переключение открытие/закрытие порта
        private void ConnectionSwitch_MI_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (port.IsOpen)
                {
                    port.Close();
                    return;
                }
                port.Open();
                StartRead();
            }
            catch
            {
                ViewModel.ErrorMessage = "Ошибка подключения: Порт не доступен/закрыт";
            }
        }

        // Перевод (HEX <-> ASCII) уже ввёдных данных по нажатию на радио-кнопку
        private void SendDataType_RB_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb) return;

            Input_TBx.Text = rb.Tag switch
            {
                "Text" => ViewModel.SelectedEncoding.GetString( HexStringToByteArray.Convert(Input_TBx.Text) ),
                _ => BitConverter.ToString( ViewModel.SelectedEncoding.GetBytes(Input_TBx.Text) ).Replace('-', ' ')
            };
        }
    }
}