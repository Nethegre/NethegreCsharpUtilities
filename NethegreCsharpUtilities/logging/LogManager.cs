using System.Collections.Concurrent;
using System.Diagnostics;
using nethegre.csharp.util.config;

namespace nethegre.csharp.util.logging
{
    public class LogManager
    {
        //Queue that will hold all logs before they are written
        private static ConcurrentQueue<Log> logQueue = new ConcurrentQueue<Log>();
        //Log file relative path
        private static string _logFile = ConfigManager.config["logFile"];
        //Global logging level
        private static LogLevel _loggingLevel = (LogLevel)Convert.ToInt32(ConfigManager.config["loggingLevel"]);
        //Sleep timer if there are no logs in the queue
        private static int _logProcessSleep = Convert.ToInt32(ConfigManager.config["logProcessSleep"]);
        //Write stream for the log file
        private static StreamWriter _logWriter = null;

        //Instance specific variables
        readonly string className;

        public LogManager(Type t)
        {
            className = t.Name;

            setupLogFile();
        }

        public LogManager(string name)
        {
            className = name;

            setupLogFile();
        }

        public void error(string message)
        {
            StackTrace st = new StackTrace();
            string logMsg = "[" + className + "." + st.GetFrame(1).GetMethod().ReflectedType.Name + "] ERROR - " + message;
            addLogToQueue(new Log(logMsg, LogLevel.ERROR));
        }

        public void warn(string message)
        {
            StackTrace st = new StackTrace();
            string logMsg = "[" + className + "." + st.GetFrame(1).GetMethod().ReflectedType.Name + "] WARN - " + message;
            addLogToQueue(new Log(logMsg, LogLevel.WARN));
        }

        public void info(string message)
        {
            StackTrace st = new StackTrace();
            string logMsg = "[" + className + "." + st.GetFrame(1).GetMethod().ReflectedType.Name + "] INFO - " + message;
            addLogToQueue(new Log(logMsg, LogLevel.INFO));
        }

        public void debug(string message)
        {
            StackTrace st = new StackTrace();
            string logMsg = "[" + className + "." + st.GetFrame(1).GetMethod().ReflectedType.Name + "] DEBUG - " + message;
            addLogToQueue(new Log(logMsg, LogLevel.DEBUG));
        }

        //Internal struct used for processing/categorizing logs
        internal struct Log
        {
            public string message;
            public LogLevel logLevel;
            public DateTime logTime;

            public Log(string message, LogLevel logLevel)
            {
                this.message = message;
                this.logLevel = logLevel;
                logTime = DateTime.Now;
            }

            public string getFormattedLog()
            {
                return logTime.ToString() + " " + message;
            }
        }

        //Enum used to categorize log levels
        internal enum LogLevel { DEBUG = 0, INFO = 1, WARN = 2, ERROR = 3 }

        //Static methods used for the background processing
        public static async Task ProcessLogs()
        {
            //Verify that the log writer is setup
            setupLogFile();

            //Sleep if the queue is empty before trying to log again
            if (logQueue.IsEmpty)
            {
                Thread.Sleep(_logProcessSleep);
            }
            else
            {
                if (logQueue.TryDequeue(out Log logToWrite))
                {
                    //Write log to console and to file
                    Console.WriteLine(logToWrite.getFormattedLog());

                    try
                    {
                        _logWriter.WriteLine(logToWrite.getFormattedLog());
                        _logWriter.Flush(); //Immediately write to the file so that it is not lost on app shutdown
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(DateTime.Now.ToString() + " [LogManager.ProcessLogs] Error - Exception while processing logs [" + ex.Message + "]");
                    }
                }
            }

            //Dispose of the _logWriter cleanly on shutdown
            _logWriter.Close();
            _logWriter.Dispose();
            _logWriter = null; //Setting this to null will allow the _logWriter to bet setup again.
        }

        internal static void addLogToQueue(Log log)
        {
            //Check the global logging level and add the log to the queue if it is 
            if (log.logLevel >= _loggingLevel)
            {
                logQueue.Enqueue(log);
            }
        }

        internal static void setupLogFile()
        {
            //Check if logging file exists and create it if it doesn't
            if (!File.Exists(_logFile))
            {
                FileStream stream = File.Create(_logFile);
                stream.Close();
                stream.Dispose(); //Release stream resources so the file is not locked
            }

            //Check if the _logWriter is null
            if (_logWriter == null)
            {
                //Grab a writeStream for the log file to lock it
                _logWriter = new StreamWriter(File.OpenWrite(_logFile));
            }
        }
    }
}
