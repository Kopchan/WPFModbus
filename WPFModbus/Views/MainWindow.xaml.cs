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

namespace WPFModbus.Views
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SerialPort port;

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
                PortSettingsWindow window = new();
                window.ShowDialog();
            }
            Init();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
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
                ViewModel.Port = port;
                port.Open();
                StartRead();
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "Для работы с программой необходимо выбрать не закрытый порт",
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
                // Остановка задачи чтения, если порт вдруг закрыт
                while (ViewModel.PortIsOpen = port.IsOpen)
                {
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
                    ReceivedLine line = new(ViewModel.ReceivedLines.Last()?.Id + 1 ?? 1, DateTime.Now, buffer);

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
        private void SendMessage(object sender, RoutedEventArgs e)
        {
            // Очистка ошибки
            ViewModel.ErrorMessage = "";

            // Отмена отправки, если она происходила
            if (ViewModel.IsSending)
            {
                port.DiscardOutBuffer();
                ViewModel.IsSending = false;
                return;
            }

            // Установка состояние отправки 
            ViewModel.IsSending = true;

            // Перевод строки ввода в байты 
            string input = Input_TBx.Text;
            byte[] buffer = ViewModel.SendDataType switch
            {
                SendDataType.ASCII => Encoding.GetEncoding(1251).GetBytes(input),
                _ => HexStringToByteArray.Convert(input)
            };

            Task.Run(() =>
            {
                Debug.WriteLine("Sending...");
                try
                {
                    // Отправка
                    port.Write(buffer, 0, buffer.Length);
                }
                catch (Exception ex)
                {
                    // Таймаут или другие ошибки
                    if (ViewModel.IsSending) ViewModel.ErrorMessage = 
                        "Ошибка при отправке: " + ex switch 
                        { 
                            TimeoutException => "Время ожидания вышло",
                            UnauthorizedAccessException => "Порт недоступен/закрыт",
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

        // Очистка таблицы
        private void ClearOutput(object sender, RoutedEventArgs e) => 
            ViewModel.ReceivedLines.Clear();

        // Очистка ошибки при вводе
        private void Input_TBx_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (InputGridRow.ActualHeight > Math.Min(InputSide.Height, Height - 200))
                InputGridRow.Height = GridLength.Auto;

            // FIXME: InputSide.ActualHeight даёт размер, не учитывая содержимое
            Debug.WriteLine($"{InputGridRow.ActualHeight > Math.Min(InputSide.ActualHeight, Height - 200)} = {InputGridRow.ActualHeight} > min({InputSide.ActualHeight}, {Height - 200})");

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
            Properties.Settings.Default.Input = Input_TBx.Text;
            Properties.Settings.Default.SendDataType = ViewModel.SendDataType.ToString();

            Properties.Settings.Default.Save();
        }

        // Восстановление размеров и положения окна, поля ввода и режимов
        private void Window_SourceInitialized(object sender, EventArgs e)
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

            InputGridRow.MaxHeight = Math.Min(InputSide.ActualHeight, Height - 200);
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
                MessageBox.Show(
                    "Для работы с программой необходимо выбрать не закрытый порт",
                    "Выберите порт",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        // Перевод (HEX <-> ASCII) уже ввёдных данных по нажатию на радио-кнопку
        private void SendDataType_RB_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb) return;

            Input_TBx.Text = rb.Content switch
            {
                // FIXME: Encoding.GetEncoding(1251) может что-то сломать?
                "ASCII" => Encoding.GetEncoding(1251).GetString( HexStringToByteArray.Convert(Input_TBx.Text) ),
                _ => BitConverter.ToString( Encoding.GetEncoding(1251).GetBytes(Input_TBx.Text) ).Replace('-', ' ')
            };
        }
    }
}