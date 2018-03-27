using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CRTTestTask
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Copier copy;
        public MainWindow()
        {
            InitializeComponent();
        }

        string defaultFileName = "";
        
        //открывает диалог выбора исходного файла
        private void BrowseFrom_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                string filename = dlg.FileName;
                textBoxFrom.Text = filename;
                defaultFileName = filename;
            }
        }

        //открывает диалог выбора места сохранения
        private void BrowseTo_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = defaultFileName.Substring(defaultFileName.LastIndexOf('\\')+1);
            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                string filename = dlg.FileName;
                textBoxTo.Text = filename;
            }
        }

        //контролирует ввод только числовых данных в полe bufferSize
        private void CheckFieldBufferSize(object sender, TextCompositionEventArgs e)
        {
            if (!Char.IsDigit(e.Text, 0))
                e.Handled = true;
        }

        //изменение буфера во время копирования
        private async void ChangeBufferSize_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                buttonChangeBufferSize.IsEnabled = false;
                int newBufferSize = Int32.Parse(textBoxBufferSize.Text);
                await Task.Factory.StartNew(
                    () => copy.changeBufferSize(newBufferSize),
                    TaskCreationOptions.LongRunning);
                buttonChangeBufferSize.IsEnabled = true;
            }
            catch (FormatException)
            {
                MessageBox.Show("Invalid character in buffer size field");
            }
        }

        //запускает копирование
        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                copy = new Copier(textBoxFrom.Text,
                    textBoxTo.Text,
                    Int32.Parse(textBoxBufferSize.Text));
            }
            catch(OverflowException)
            {
                //под х32 сборку ввести ограничение размера буфера <1гб
                MessageBox.Show("Buffer size's can't be more than 2Gb");
                return;
            }
            catch (FormatException)
            {
                MessageBox.Show("Invalid character in buffer size field");
                return;
            }
            buttonCopy.IsEnabled = false;
            buttonChangeBufferSize.IsEnabled = true;
            readControl.IsEnabled = true;
            writeControl.IsEnabled = true;

            //для обновления UI (уровень заполнения буфера, статус потоков) из дочерних потоков
            Progress<int> progress = new Progress<int>(s => bufferFilling.Value = s);
            Progress<string> readStatus = new Progress<string>(s => readStreamStatus.Content = s),
                             writeStatus = new Progress<string>(s => writeStreamStatus.Content = s);

            Task.Factory.StartNew(
                () => copy.getStreamStatus(readStatus, writeStatus),
                TaskCreationOptions.LongRunning);

            Task.Factory.StartNew(
                () => copy.longRead(progress),
                TaskCreationOptions.LongRunning);

            //дождаться конца записи из буфера
            await Task.Factory.StartNew(
                () => copy.longWrite(progress),
                TaskCreationOptions.LongRunning);

            readControl.IsEnabled = false;
            writeControl.IsEnabled = false;
            buttonChangeBufferSize.IsEnabled = false;
            buttonCopy.IsEnabled = true;
        }

        //остановить/продолжить поток чтения
        private void readControl_Click(object sender, RoutedEventArgs e)
        {
            if (copy.changeReadStatus())
                readControl.Content = "Start reading";
            else
                readControl.Content = "Stop reading";
        }

        //остановить/продолжить поток записи
        private void writeControl_Click(object sender, RoutedEventArgs e)
        {
            if (copy.changeWriteStatus())
                writeControl.Content = "Start writing";
            else
                writeControl.Content = "Stop writing";
        }
    }
}
