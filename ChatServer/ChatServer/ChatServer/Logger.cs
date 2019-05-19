using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace ChatServer
{
    class Logger
    {
        private static AutoResetEvent loggerEvent = new AutoResetEvent(false);
        private static AutoResetEvent loggerExcEvent = new AutoResetEvent(false);
        private static Queue<string> loggingQueue = new Queue<string>();
        private static Object loggingQueueObj = new object();
        private static Object logptrLock = new object();
        private static Object logptrLock2 = new object();
        private static Queue<string> loggingQueueTemp = new Queue<string>();
        private static Queue<string> loggingEXQueue = new Queue<string>();
        private static Object loggingEXQueueObj = new Object();
        private static Queue<string> loggingEXQueueTemp = new Queue<string>();
        public static StreamWriter logptr;
        public static StreamWriter logExptr;
        public static string Logname;
        public static string Path;
        public static string ExName;
        public static int Counter =0;

        public void initiateLogger(string path,string logname, string Ex ,string exname)
        {
            ++Counter;

            Path = path;
            Logname = logname;
            ExName = exname;
            FileStream fcreate = File.Open(path + logname + Counter + ".txt", FileMode.Create);
            FileStream dcraete = File.Open(Ex + exname + Counter + ".txt", FileMode.Create);
            logptr = new StreamWriter(fcreate);
            logExptr = new StreamWriter(dcraete);

            logptr.Flush();
            logExptr.Flush();
            logptr.AutoFlush = true;
            logExptr.AutoFlush = true;
           
           Thread loggerThread2 = new Thread(QueuedLogging);
            loggerThread2.Start();

            Thread loggerThread3 = new Thread(QueuedLoggingEx);
            loggerThread3.Start();

            Thread loggerThread4 = new Thread(CheckFileSize);
            loggerThread4.Start();

        }
        public  void QueuedLogging()
        {
            while (true)
            {

               
                    try
                    {
                    string logMessage = string.Empty;
                    Queue<string> swap = new Queue<string>();
                    loggerEvent.WaitOne();
                    lock(loggingQueueObj)
                    {
                        swap = loggingQueue;
                        loggingQueue = loggingQueueTemp;
                        loggingQueueTemp = swap;
                        
                    }
                    while (loggingQueueTemp.Count > 0)
                    {

                       
                            try
                            {
                            lock (logptrLock2)
                            {

                                logMessage = loggingQueueTemp.Dequeue();
                                logptr.WriteLine("{0} {1}", DateTime.Now.ToLongTimeString(),
                                DateTime.Now.ToLongDateString());
                                logptr.WriteLine("  :");
                                logptr.WriteLine("  :{0}", logMessage);
                                if (loggingQueueTemp.Count > 10000)
                                {
                                    logptr.Write("\r\n FLUSHING QUEUE WITH MESSAGES COUNT : :" + loggingQueueTemp.Count);

                                    loggingQueueTemp.Clear();
                                }
                            }
                            }
                            catch(Exception ex)
                            {
                                Console.WriteLine("Creating New File");
                            }
                        
                    }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception occurred while logging :" + ex.GetBaseException());
                    }

                

            }
        }

        public void CheckFileSize()
        {
            while (true)
            {
                long length = logptr.BaseStream.Length;
                long length2 = logExptr.BaseStream.Length;
                ++Counter;
                if (((length / 1024)/1024) >= 200)
                {
                    lock(logptrLock)
                    {
                        logptr.BaseStream.SetLength(0);
                        logptr.BaseStream.Close();
                        FileStream fcreate = File.Open(Path + Logname + Counter + ".txt", FileMode.Create);

                        logptr = new StreamWriter(fcreate);


                        logptr.Flush();
                        logptr.AutoFlush = true;
                    }
                    // This flushes the content, too.
                                               //  logptr.Close();

                   

                   

                }

                if ((length2 / 1024)/1024 >= 200)
                {
                    lock (logptrLock2)
                    {


                        logExptr.BaseStream.SetLength(0);
                        logExptr.BaseStream.Close(); // This flushes the content, too.


                        //  logExptr = File.AppendText(Path + ExName + Counter + ".txt");
                        FileStream dcraete = File.Open(Path + ExName + Counter + ".txt", FileMode.Create);

                        logExptr = new StreamWriter(dcraete);

                        logExptr.Flush();

                        logExptr.AutoFlush = true;
                    }
                }
           
                Thread.Sleep(5000);
            }
            }
        public void QueuedLoggingEx()
        {
            while (true)
            {
               string logMessage = string.Empty;
                Queue<string> swap = new Queue<string>();
                loggerExcEvent.WaitOne();
                lock (loggingEXQueueObj)
                {
                    swap = loggingEXQueue;
                    loggingEXQueue = loggingEXQueueTemp;
                    loggingEXQueueTemp = swap;

                }
                while (loggingEXQueueTemp.Count > 0)
                {
                    try
                    {
                        logMessage = loggingEXQueueTemp.Dequeue();

                        //  string logLine = System.String.Format("{0:G}: {1} :.", System.DateTime.Now, logMessage);
                        lock (logptrLock2)
                        {
                            logExptr.WriteLine("{0} {1}", DateTime.Now.ToLongTimeString(),
                                DateTime.Now.ToLongDateString());
                            logExptr.WriteLine("  :");
                            logExptr.WriteLine("{0}/n", logMessage);
                            if (loggingEXQueueTemp.Count > 10000)
                            {
                                logExptr.Write("\r\n FLUSHING QUEUE WITH MESSAGES COUNT : :" + loggingEXQueueTemp.Count);

                                loggingEXQueueTemp.Clear();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Craeting new Log FIle");
                    }
                    
                }
                

            }
        }
        public void info(string text)
        {
            lock (loggingQueueObj)
            {
                loggingQueue.Enqueue(text);
                loggerEvent.Set();
            }
            }

        public void error(string text)
        {
            lock (loggingEXQueueObj)
            {
                loggingEXQueue.Enqueue(text);
                loggerExcEvent.Set();
            }
        }
    }
}
