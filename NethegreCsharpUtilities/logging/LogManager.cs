using System.Collections.Concurrent;
using System.Diagnostics;
using nethegre.csharp.util.config;

namespace nethegre.csharp.util.logging
{
    /// <summary>
    /// A class that provides simple logging functionality.
    /// </summary>
    public class LogManager
    {
        //Queue that will hold all logs before they are written
        private static ConcurrentQueue<Log> logQueue = new ConcurrentQueue<Log>();
        //Log file relative path
        private static string _logFile = ConfigManager.config["logFile"];
        //Static log level that will apply to every instance of the logger unless overridden
        private static LogLevel _loggingLevel = (LogLevel)Convert.ToInt32(ConfigManager.config["loggingLevel"]);
        //Sleep timer if there are no logs in the queue
        private static int _logProcessSleep = Convert.ToInt32(ConfigManager.config["logProcessSleep"]);
        //String that will be appended to the log file when the log manager creates the log file.
        private static string _logFileCreateLine = ConfigManager.config["logFileCreateLine"];
        //Write stream for the log file
        private static StreamWriter _logWriter = null;
        //Used to stop the log processing
        private static bool _shutdown = false;
        //Used to track if the log processing has been started or not
        private static Task _backgroundProcessing = null;

        //Instance specific variables
        readonly string className;
        readonly LogLevel instanceSpecificLogLevel;

        /// <summary>
        /// Default constructor for the log manager, inherits the log level from the
        /// global setting.
        /// </summary>
        /// <param name="t"></param>
        public LogManager(Type t) : this()
        {
            className = t.Name;
            instanceSpecificLogLevel = _loggingLevel;

            setupLogFile();
        }

        /// <summary>
        /// Default constructor for the log manager, inherits the log level from the
        /// global setting.
        /// </summary>
        /// <param name="name"></param>
        public LogManager(string name) : this()
        {
            className = name;

            setupLogFile();
            instanceSpecificLogLevel = _loggingLevel;
        }

        /// <summary>
        /// Allows for the setting of a specific logging level for the instance of the 
        /// logging class. This constructor will override the default logging level.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="loggingLevel"></param>
        public LogManager(string name, LogLevel loggingLevel) : this()
        {
            this.className = name;
            this.instanceSpecificLogLevel = loggingLevel;
            setupLogFile();
        }

        /// <summary>
        /// Allows for the setting of a specific logging level for the instance of the 
        /// logging class. This constructor will override the default logging level.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="loggingLevel"></param>
        public LogManager(Type t, LogLevel loggingLevel) : this()
        {
            this.className = t.Name;
            this.instanceSpecificLogLevel = loggingLevel;
            setupLogFile();
        }

        /// <summary>
        /// Internal constructor that starts the log processing. This guarentees
        /// that as soon as you instantiate one of the LogManagers that you have
        /// logs written.
        /// </summary>
        private LogManager() 
        {
            //Check to make sure that the className is not null
            if (className == null)
            {
                className = this.GetType().Name;
            }

            //Start the log processing here but only if it hasn't been started yet
            if (!_shutdown)
            {
                //Check to make sure another background process hasn't been started
                if (_backgroundProcessing == null)
                {
                    //Start the processing task
                    _backgroundProcessing = Task.Run(ProcessLogs);
                }
                else
                {
                    //Check that the background task has not failed for some reason
                    if (_backgroundProcessing.IsCanceled || _backgroundProcessing.IsFaulted || _backgroundProcessing.IsCompleted)
                    {
                        //Dispose of the background thread so we don't cause a memory leak or something
                        _backgroundProcessing.Dispose();

                        //Relace the background processing thread with a new instance because it should never stop
                        _backgroundProcessing = Task.Run(ProcessLogs);
                    }
                }
            }
        }

        /// <summary>
        /// Logs with the LogLevel of ERROR
        /// </summary>
        /// <param name="message"></param>
        public void error(string message)
        {
            StackTrace st = new StackTrace();
            string logMsg = "[" + className + "." + st.GetFrame(1).GetMethod().ReflectedType.Name + "] ERROR - " + message;
            addLogToQueue(new Log(logMsg, LogLevel.ERROR));
        }

        /// <summary>
        /// Logs with the LogLevel of WARN
        /// </summary>
        /// <param name="message"></param>
        public void warn(string message)
        {
            StackTrace st = new StackTrace();
            string logMsg = "[" + className + "." + st.GetFrame(1).GetMethod().ReflectedType.Name + "] WARN - " + message;
            addLogToQueue(new Log(logMsg, LogLevel.WARN));
        }

        /// <summary>
        /// Logs with the LogLevel of INFO
        /// </summary>
        /// <param name="message"></param>
        public void info(string message)
        {
            StackTrace st = new StackTrace();
            string logMsg = "[" + className + "." + st.GetFrame(1).GetMethod().ReflectedType.Name + "] INFO - " + message;
            addLogToQueue(new Log(logMsg, LogLevel.INFO));
        }

        /// <summary>
        /// Logs with the LogLevel of DEBUG
        /// </summary>
        /// <param name="message"></param>
        public void debug(string message)
        {
            StackTrace st = new StackTrace();
            string logMsg = "[" + className + "." + st.GetFrame(1).GetMethod().ReflectedType.Name + "] DEBUG - " + message;
            addLogToQueue(new Log(logMsg, LogLevel.DEBUG));
        }

        /// <summary>
        /// Internal struct used for processing/categorizing logs.
        /// </summary>
        internal struct Log
        {
            public string message;
            public LogLevel logLevel;
            public DateTime logTime;

            public Log(string message, LogLevel logLevel)
            {
                this.message = message;
                this.logLevel = logLevel;
                this.logTime = DateTime.Now;
            }

            public string getFormattedLog()
            {
                return logTime.ToString() + " " + message;
            }
        }

        /// <summary>
        /// Used to define the supported logging levels.
        /// </summary>
        public enum LogLevel { DEBUG = 0, INFO = 1, WARN = 2, ERROR = 3 }

        /// <summary>
        /// This will stop log processing for the log manager.
        /// </summary>
        public static void shutdown()
        {
            //Sleep for sometime before returning to give the process time to write the remaining logs to a file
            Thread.Sleep(_logProcessSleep * 2);
            _shutdown = true;
            Thread.Sleep(_logProcessSleep * 4);
        }

        /// <summary>
        /// This is no longer static because we need it to interact with the 
        /// instance specific logging level so that each class specific instance 
        /// of the logger can have its own logging level
        /// </summary>
        /// <param name="log"></param>
        internal void addLogToQueue(Log log)
        {
            //Check the global logging level and add the log to the queue if it is 
            if (log.logLevel >= instanceSpecificLogLevel)
            {
                logQueue.Enqueue(log);
            }
        }

        /// <summary>
        /// Method that is used to do the main log processing.
        /// </summary>
        /// <returns></returns>
        internal static async Task ProcessLogs()
        {
            //Verify that the log writer is setup
            setupLogFile();

            while (!_shutdown)
            {
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
                            await _logWriter.WriteLineAsync(logToWrite.getFormattedLog());
                            await _logWriter.FlushAsync(); //Immediately write to the file so that it is not lost on app shutdown
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(DateTime.Now.ToString() + " [LogManager.ProcessLogs] Error - Exception while processing logs [" + ex.Message + "]");
                        }
                    }
                }
            }

            //Dispose of the _logWriter cleanly on shutdown
            _logWriter.Close();
            await _logWriter.DisposeAsync();
            _logWriter = null; //Setting this to null will allow the _logWriter to be setup again.
        }

        /// <summary>
        /// The basic method that verifies that the log file exists and creates it if it doesn't.
        /// </summary>
        internal static void setupLogFile()
        {
            //Check if logging file exists and create it if it doesn't
            if (!File.Exists(_logFile))
            {
                File.AppendAllText(LogManager._logFile, String.Format(_logFileCreateLine, DateTime.Now));
                //FileStream stream = File.Create(_logFile);
                //stream.Close();
                //stream.Dispose(); //Release stream resources so the file is not locked
            }

            //Check if the _logWriter is null
            if (_logWriter == null)
            {
                //Grab a writeStream for the log file to lock it
                _logWriter = new StreamWriter((Stream)File.Open(_logFile, FileMode.Append, FileAccess.Read, FileShare.Read));
            }
        }
    }
}
