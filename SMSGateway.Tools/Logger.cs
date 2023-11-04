using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.Tools
{
    public enum LogType
    {
        Pdu = 1,
        Steps = 2,
        Warning = 4,
        Error = 8,
        Exceptions = 16,
        Information = 32
    }

    public class Logger
    {
        private LogType LogLevel { get; set; }
        private string LogDirectory = "Logs";
        private string AppPath;
        private string FileName { get; set; }
        private object fileLock = new object();

        public Logger(LogType logType, string filename)
        {
            LogLevel = logType;
            AppPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            //LogDirectory = "Logs";
            FileName = Path.Combine(AppPath, filename) ;
            //Path.Combine(AppPath, LogDirectory, System.DateTime.Now.ToString("yyyyMMdd") + ".txt");
        }

        public Logger(LogType logType, string path, string filename)
        {
            LogLevel = logType;
            AppPath = path;
            FileName = Path.Combine(path, filename);
        }

        public async Task Write(LogType logType, string message, CancellationToken cancellationToken)
        {
            if (logType < LogLevel)
                return;

            if (String.IsNullOrEmpty(message))
                return;

            StringBuilder messageText = new StringBuilder();
            messageText
                .Append(String.Format("{0:dd-MM-yyyy HH:mm:ss.ffffff}", DateTime.Now))
                .Append($" {logType.ToString().ToUpper()} : {message}");
            //Write(logType, string.Empty, messageText);
            //StringBuilder sb = new StringBuilder();
            //sb.Append(DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss.fff"));
            //sb.Append(" ");
            //Task.Run(async () => { await WriteLog(logType, messageText); });

            #region [ Write Log ]
            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(Path.Combine(AppPath, LogDirectory));
                if (!dirInfo.Exists)
                    dirInfo.Create();

                //string fileName = Path.Combine(AppPath, LogDirectory, System.DateTime.Now.ToString("yyyyMMdd") + ".txt");

                //lock (fileLock)
                {
                    //bool success = false;
                    int retryIndex = 0;
                    FileStream file;
                    do
                    {
                        try
                        {
                            //File.AppendAllText(fileName, message + Environment.NewLine);
                            FileMode fileMode = File.Exists(this.FileName) ? FileMode.Append : FileMode.OpenOrCreate;
                            //FileStream file = new FileStream(this.FileName, fileMode, FileAccess.Write);
                            //FileStream 
                            file = new FileStream(
                                    this.FileName,
                                    fileMode, FileAccess.Write, FileShare.None,
                                    bufferSize: 4096, useAsync: true);

                            using (StreamWriter streamWriter = new StreamWriter(file, Encoding.UTF8))
                            {
                                await streamWriter.WriteAsync(messageText, cancellationToken);
                                streamWriter.Close();
                            }
                            file.Close();
                            //success = true;
                            break;
                        }
                        catch (Exception ex)
                        {
                        }
                        finally
                        {
                            
                        }
                    } while (retryIndex < 5);
                }
            }
            catch
            {
            }
            #endregion

        }
        //public static void Write(LogType logType, string message1, string message2)
        //private async Task WriteLog(LogType logType, string message)
        //{
        //    try
        //    {
        //        DirectoryInfo dirInfo = new DirectoryInfo(Path.Combine(AppPath, LogDirectory));
        //        if (!dirInfo.Exists)
        //            dirInfo.Create();

        //        string fileName = Path.Combine(AppPath, LogDirectory, System.DateTime.Now.ToString("yyyyMMdd") + ".txt");

        //        lock (fileLock)
        //        {
        //            bool success = false;
        //            do
        //            {
        //                try
        //                {
        //                    //File.AppendAllText(fileName, message + Environment.NewLine);
        //                    FileMode fileMode = File.Exists(fileName) ? FileMode.Append : FileMode.OpenOrCreate;
        //                    FileStream file = new FileStream(fileName, fileMode, FileAccess.Write);

        //                    using (StreamWriter streamWriter = new StreamWriter(file, Encoding.UTF8))
        //                    {
        //                        streamWriter.WriteLine(message);
        //                        streamWriter.Close();
        //                    }
        //                    file.Close();
        //                    success = true;
        //                }
        //                catch (Exception ex)
        //                {
        //                }
        //            } while (!success);
        //        }

        //        //string messageText = String.Format("{0:dd-MM-yyyy HH:mm:ss.fff} {1} :", DateTime.Now, logType.ToString().ToUpper());

        //        //if (!String.IsNullOrEmpty(message))
        //        //    messageText = String.Format("{0} {1}", messageText, message);

        //        //if (!String.IsNullOrEmpty(message2))
        //        //    messageText = String.Format("{0} {1}", messageText, message2);

        //        //if (Monitor.TryEnter(fileLock, 1000))
        //        //{
        //        //    try
        //        //    {
        //        //        if (!File.Exists(fileName))
        //        //        {
        //        //            FileStream file1 = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.Write);
        //        //            StreamWriter streamWriter1 = new StreamWriter(file1, Encoding.UTF8);
        //        //            streamWriter1.Close();
        //        //            file1.Close();
        //        //        }

        //        //        FileStream file = new FileStream(fileName, FileMode.Append, FileAccess.Write);
        //        //        StreamWriter streamWriter = new StreamWriter(file, Encoding.UTF8);
        //        //        streamWriter.WriteLine(messageText);
        //        //        streamWriter.Close();
        //        //        file.Close();
        //        //    }
        //        //    finally
        //        //    {
        //        //        Monitor.Exit(fileLock);
        //        //    }
        //        //}
        //        //Task.Run(async () => { await WriteLog(fileName, messageText); });

        //        //await WriteLog(fileName, messageText);
        //    }
        //    catch
        //    {
        //    }
        //}
    }
}
