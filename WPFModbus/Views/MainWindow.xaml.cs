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
                for (ulong i = 0;;)
                {
                    // Остановка задачи чтения, если порт вдруг закрыт
                    ViewModel.PortIsOpen = port.IsOpen;
                    if (!ViewModel.PortIsOpen) break;

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
                    ReceivedLine line = new(i, DateTime.Now, buffer);

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
                    if (ViewModel.IsSending) ViewModel.ErrorMessage = 
                        "Ошибка при отправке: " + ex switch { 
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
            PortSettingsWindow window = new();
            window.Owner = this;
            window.Left = Left + (ActualWidth  - window.Width ) / 2;
            window.Top  = Top  + (ActualHeight - 300) / 2;

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
        private void Input_TBx_TextChanged(object sender, TextChangedEventArgs e) =>
            ViewModel.ErrorMessage = "";

        // Запись размеров и положения окна во время закрытия для последущего восстановление
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            { 
                // Берём прошлые размеры
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

            Properties.Settings.Default.Save();
        }

        // Восстановление размеров и положения окна
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            if (Height > 0) // Если не по умолчанию
            {
                Top    = Properties.Settings.Default.Top;
                Left   = Properties.Settings.Default.Left;
                Height = Properties.Settings.Default.Height;
                Width  = Properties.Settings.Default.Width;
            }

            if (Properties.Settings.Default.Maximized)
                WindowState = WindowState.Maximized;
        }

        private void ConnectionSwitch_MI_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (port.IsOpen)
                {
                    port.Close();
                    //ConnectionSwitch_MI.Header = "_Подключиться";
                    return;
                }
                //ConnectionSwitch_MI.Header = "_Отключиться";
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
    }
}