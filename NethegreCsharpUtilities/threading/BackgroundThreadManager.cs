using nethegre.csharp.util.logging;
using nethegre.csharp.util.config;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace nethegre.csharp.util.threading
{
    /// <summary>
    /// A class that provides a simple interface for running threads in the background.
    /// </summary>
    public static class BackgroundThreadManager
    {
        //Create instance of the log manager class
        readonly static LogManager log = new LogManager(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        //Thread safe dictionary to store all background threads that have been started
        //private static ConcurrentDictionary<string, Thread> threads = new ConcurrentDictionary<string, Thread>(); //Not using this for now
        private static ConcurrentDictionary<string, Task> tasks = new ConcurrentDictionary<string, Task>();

        //Config variables
        public static readonly int numberOfAttemptsToAddToDictionary;

        //Default constructor that sets up config variables
        static BackgroundThreadManager()
        {
            //Need to check that the values for the config options are set
            numberOfAttemptsToAddToDictionary = Convert.ToInt32(ConfigManager.config["numberOfThreadAttempts"] ?? "5");
        }

        /// <summary>
        /// Starts a task via the provided function. If a task with the same name has been provided already it returns false.
        /// Also if there are any errors/exceptions when starting the task it will return false. Otherwise returns true.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="function"></param>
        /// <returns></returns>
        public static bool startBackgroundTask(string name, Func<Task?> function)
        {
            //Check to make sure that the name and function provided are not null
            if (name == null) { throw new ArgumentNullException("name"); }
            validateFunctionOrMethodNameNotNull(function);

            //Check if a task with the provided name existed in the dictionary
            if (tasks.ContainsKey(name)) { return false; }
            else
            {
                try
                {
                    //Run the function as a task
                    Task task = Task.Run(function);

                    //Place the task in the concurrent dictionary
                    if (tasks.TryAdd(name, task))
                    {
                        log.debug("Successfully added function [" + function.Method.Name + "] to dictionary with placeholder name [" + name + "]");
                        return true;
                    }
                    else
                    {
                        //Need to make sure to dispose of the task so we don't create a memory leak
                        task.Dispose();

                        return false;
                    }
                }
                catch (Exception ex) 
                {
                    log.error("Failed to add task with name [" + name + "] to the dictionary due to exception [" + ex.Message + "]");
                    return false;
                }
            }
        }
        
        /// <summary>
        /// Starts a task via the provided function and returns the registered name associated with it
        /// so that it can be managed/queried later. 
        /// NOTE: The registered name is generated via Guid.NewGuid().ToString()
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public static string startBackgroundTask(Func<Task?> function)
        {
            //Generate a randomized name via a guid
            string name = Guid.NewGuid().ToString();

            //Validate that function is not null and throw exception if it is
            validateFunctionOrMethodNameNotNull(function);

            try
            {
                //Start the task 
                Task task = Task.Run(function);
                int counter = 0;

                //Attempt to add the items to the dictionary if not try again
                while (!tasks.TryAdd(name, task))
                {
                    if (counter >= numberOfAttemptsToAddToDictionary)
                    {
                        log.warn("Failed to add function [" + function.Method.Name + "] with name [" + name + "] to the dictionary, max number of attemps reached.");

                        return "";
                    }

                    //Need to generate a new name otherwise we will loop infinitely
                    name = Guid.NewGuid().ToString();

                    counter++;
                }
            }
            catch (Exception ex)
            {
                log.error("Exception while starting task [" + function.Method.Name + "] or adding it to the dictionary [" + ex.Message + "]");
                throw;
            }

            return name;
        }

        //TODO Should create a method that disposes of an existing background task/thread if one is created after with the same name

        //TODO Need to create a method to retrieve the status of a registered task


        //Need to create a struct that holds name/behaviour information (replaceable, or similar boolean)
        // that companions that actual Task in the Dictionary? This seems overly complicated but would it be helpful?

        //Helper methods below

        /// <summary>
        /// Validates that the provided function and its Method.Name are not null. Throws
        /// ArgumentNullException if they are.
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        internal static void validateFunctionOrMethodNameNotNull(Func<Task?> function)
        {
            //Check for nulls
            if (function == null) { throw new ArgumentNullException("function"); }
            if (function.Method == null) { throw new ArgumentNullException("function.Method"); }
            if (function.Method.Name == null) { throw new ArgumentNullException("function.Method.Name"); }
        }


    }
}
