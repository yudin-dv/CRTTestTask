using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CRTTestTask
{
    class Copier
    {
        private Queue<BufferPart> buffer;
        string SourceFile, DestFile;
        int bufferSize, 
            blockSize=4096,//размер одного блока для потоков чтения/записи в кбайт
            blocksCount;
        bool readSetPause = false,
             writeSetPause = false,
             readWaiting = true,
             writeWaiting = true;
        
        public Copier(string SourceFile, string DestFile, int bufferSize)
        {
            this.SourceFile = SourceFile;
            this.DestFile = DestFile;
            this.bufferSize = bufferSize;

            /*буффер - очередь из блоков размера blockSize количестом bufferSize / blockSize + 1
             * побайтовое считывание FileStream.ReadByte() неэффективно даёт огромную нагрузку
             * на процессор при малой производительности. К тому же, этот метод сам по себе подгружает
             * массив байт из файла, после чего выдаёт байты из него:
             * https://msdn.microsoft.com/ru-ru/library/system.io.filestream.readbyte(v=vs.110).aspx
             */
            blocksCount = bufferSize / blockSize + 1;
            buffer = new Queue<BufferPart>(blocksCount);
        }

        //реализация чтения блоков из файла в буфер
        public void longRead(IProgress<int> progress)
        {
            try
            {
                using (FileStream SourceStream = File.Open(SourceFile, FileMode.Open, FileAccess.Read))
                {
                    BufferPart part;
                    int reportCounter = 0;
                    while (true)
                    {
                        //если поток приостановлен вручную
                        if (readSetPause)
                        {
                            readWaiting = true;
                            Thread.Sleep(1000);
                            continue;
                        }
                        //если буфер не полон
                        if (buffer.Count < blocksCount)
                        {
                            part = new BufferPart(blockSize, false);
                            //если файл закончился
                            if ((part.count = SourceStream.Read(part.bytes, 0, blockSize)) == 0)
                            {
                                readWaiting = true;
                                lock (buffer)//последний пустой блок
                                    buffer.Enqueue(new BufferPart(0, true));
                                break;
                            }

                            lock (buffer)
                                buffer.Enqueue(part);
                            //обновления уровня заполненности буфера
                            //раз в сто итерация - дабы не вызывать избыточное количество репортов (страдает и скорость, и нагрузка)
                            if (reportCounter++ > 100)
                            {
                                readWaiting = false;
                                reportCounter = 0;
                                progress.Report(100 * buffer.Count / blocksCount);
                            }
                        }
                        //буфер полон, ожидание освобождения места
                        else
                        {
                            readWaiting = true;
                            Thread.Sleep(1);
                        }
                    }
                }
            }
            catch(FileNotFoundException)
            {
                buffer.Enqueue(new BufferPart(0, true));
                MessageBox.Show("File not found");
            }
            catch(ArgumentException)
            {
                buffer.Enqueue(new BufferPart(0, true));
                MessageBox.Show("Incorrect source address");
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("File's protected or readonly");
            }
        }

        //реализация записи блоков из буфера в файл
        public void longWrite(IProgress<int> progress)
        {
            try
            {
                using (FileStream DestinationStream = File.Create(DestFile))
                {
                    BufferPart onePart;
                    int reportCounter = 0;
                    while (true)
                    {
                        //если поток приостановлен вручную
                        if (writeSetPause)
                        {
                            writeWaiting = true;
                            Thread.Sleep(1000);
                            continue;
                        }
                        //если поток не пуст
                        if (buffer.Count != 0)
                        {
                            lock (buffer)
                                onePart = buffer.Dequeue();
                            //последний блок, закрытие потока 
                            if (onePart.isLastPart == true)
                            {
                                writeWaiting = true;
                                break;
                            }
                            DestinationStream.Write(onePart.bytes, 0, onePart.count);
                            //обновление уровня заполненности буфера
                            if (reportCounter++ > 100)
                            {
                                writeWaiting = false;
                                reportCounter = 0;
                                progress.Report(100 * buffer.Count / blocksCount);
                            }
                        }
                        //поток пуст, ожидание поступления новых данных
                        else
                        {
                            writeWaiting = true;
                            Thread.Sleep(1);
                        }
                    }
                }
            }
            catch (ArgumentException)
            {
                MessageBox.Show("Incorrect destination address");
            }
        }

        //обновление состояний потокв чтения/записи в UI
        public void getStreamStatus(IProgress<string> read, IProgress<string> write)
        {
            while (true)
            {
                if (readWaiting == false)
                    read.Report("Read stream: working");
                else
                    read.Report("Read stream: waiting");

                if (writeWaiting == false)
                    write.Report("Write stream: working");
                else
                    write.Report("Write stream: waiting");

                Thread.Sleep(100);
            }
        }

        //изменение размера буфера во время копирования
        public void changeBufferSize(int newSize)
        {
            if (newSize == bufferSize) return;
            
            //остановка чтения
            readSetPause = true;
            
            //ожидание освобождения буфера
            while (buffer.Count != 0)
            {
                Thread.Sleep(10);
            }
            writeSetPause = true;

            //создание нового буфера
            blocksCount = newSize / blockSize + 1;
            buffer = new Queue<BufferPart>(blocksCount);

            //запуск потоков
            readSetPause = false;
            writeSetPause = false;
        }

        //ручная приостановка потока чтения
        public bool changeReadStatus()
        {
            if (readSetPause == false)
                return readSetPause = true;
            else
                return readSetPause = false;
        }

        //ручная приостановка потока записи
        public bool changeWriteStatus()
        {
            if (writeSetPause == false)  
                return writeSetPause = true;
            else
                return writeSetPause = false;
        }

    }
}
