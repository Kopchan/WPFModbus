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
                        2 => StopBits.Two,
                        1.5 => StopBits.OnePointFive,
                        _ => StopBits.One,
                    },
                    Parity = Properties.Settings.Default.PortParity switch
                    {
                        "odd" => System.IO.Ports.Parity.Odd,
                        "even" => System.IO.Ports.Parity.Even,
                        "mark" => System.IO.Ports.Parity.Mark,
                        "space" => System.IO.Ports.Parity.Space,
                        _ => System.IO.Ports.Parity.None,
                    },
                    WriteTimeout = Properties.Settings.Default.PortTimeout,
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
                //if (enc.GetEncoding().IsSingleByte && enc.CodePage != 20127)
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
                for (ulong i = ViewModel.ReceivedLines.LastOrDefault()?.Id + 1 ?? 1; 
                     ViewModel.PortIsOpen = port.IsOpen;)
                {
                    // Усыпление задачи, если нет байтов для чтения
                    int bytes = port.BytesToRead;
                    if (bytes < 1) 
                    {
                        Thread.Sleep(100);
                        continue;
                    }
                    i++;

                    // Чтение полученных данных
                    byte[] buffer = new byte[bytes];
                    port.Read(buffer, 0, bytes);
                    ReceivedLine line = new(i, DateTime.Now, buffer, ViewModel.SelectedEncoding);

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
        private void StartSend(object sender, RoutedEventArgs e)
        {
            // Очистка ошибки
            ViewModel.ErrorMessage = "";

            // Отмена отправки, если она происходила
            if (ViewModel.IsSending || ViewModel.IsSendingInterval)
            {
                port.DiscardOutBuffer();
                ViewModel.IsSending = ViewModel.IsSendingInterval = false;
                sendTimer.Enabled = false;
                return;
            }

            // Перевод строки ввода в байты 
            string input = Input_TBx.Text;
            sendBuffer = ViewModel.SendDataType switch
            {
                SendDataType.Text => Encoding.GetEncoding(ViewModel.SelectedEncoding?.CodePage ?? 20127).GetBytes(input),
                _ => HexStringToByteArray.Convert(input)
            };

            SendOneMessage();

            if (ViewModel.SendIsInterval)
            {
                ViewModel.IsSendingInterval = true;
                sendTimer.Interval = ViewModel.SendInterval;
                sendTimer.Enabled = true;
            }
        }

        private void OnSendTimerEvent(object? source, System.Timers.ElapsedEventArgs e) => SendOneMessage();

        private void SendOneMessage()
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
                ViewModel.SendInterval = Properties.Settings.Default.SendInterval;

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
                // FIXME: Encoding.GetEncoding(1251) может что-то сломать?
                "Text" => ViewModel.SelectedEncoding.GetString( HexStringToByteArray.Convert(Input_TBx.Text) ),
                _ => BitConverter.ToString( ViewModel.SelectedEncoding.GetBytes(Input_TBx.Text) ).Replace('-', ' ')
            };
        }

        private void SendMode_RB_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}