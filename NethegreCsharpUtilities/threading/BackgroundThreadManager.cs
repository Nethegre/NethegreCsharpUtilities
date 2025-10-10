using nethegre.csharp.util.logging;
using nethegre.csharp.util.config;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Linq;

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

        //Shutdown variable
        private static bool _shutdown = false;
        private static Task? _backgroundProcessing = null;

        //Config variables
        public static readonly int _numberOfAttemptsToAddToDictionary;
        public static readonly int _backgroundThreadSleepTime;

        //Default constructor that sets up config variables
        static BackgroundThreadManager()
        {
            //Need to check that the values for the config options are set
            _numberOfAttemptsToAddToDictionary = Convert.ToInt32(ConfigManager.config["numberOfThreadAttempts"] ?? "5");
            _backgroundThreadSleepTime = Convert.ToInt32(ConfigManager.config["backgroundThreadSleepTime"] ?? "50");

            //Startup the cleanupCompletedTask background task but we need to make sure this can't happen twice
            lock (_backgroundProcessing)
            {
                if (_backgroundProcessing == null)
                {
                    log.Debug("Started up the cleanupCompletedTasks processes after it was null.");

                    //Run the cleanup task in the background
                    _backgroundProcessing = Task.Run(CleanupCompletedTasks);
                }
                else
                {
                    //Check that the background task has not failed for some reason
                    if (_backgroundProcessing.IsCanceled || _backgroundProcessing.IsFaulted || _backgroundProcessing.IsCompleted)
                    {
                        //Dispose of the background thread so we don't cause a memory leak or something
                        _backgroundProcessing.Dispose();

                        //Relace the background processing thread with a new instance because it should never stop
                        _backgroundProcessing = Task.Run(CleanupCompletedTasks);
                    }
                }
            }
        }

        /// <summary>
        /// Designed to run in the background and check for Faulted, Completed, or null
        /// background tasks in the dictionary. Will remove those entries from the dictionary
        /// if it can.
        /// </summary>
        /// <returns></returns>
        private static async Task CleanupCompletedTasks()
        {
            while (!_shutdown)
            {
                //Loop through each item in the dictionary and check the status of the task
                List<string> taskNames = new List<string> (tasks.Keys); //Gonna just take a snapshot of the keys so that it doesn't grow if other things are adding to the list

                //Loop through each of the tasks and check its status
                foreach (string name in taskNames)
                {
                    //Verify that the key is not null
                    if (name != null)
                    {
                        //Attempt to retrieve the value for the name
                        if (tasks.TryGetValue(name, out Task? task))
                        {
                            //Verify that the task is not null
                            if (task != null)
                            {
                                //Check the statuses of the task
                                if (task.IsCompletedSuccessfully || task.IsCompleted)
                                {
                                    //The task is completed so we remove it from the dictionary
                                    log.Debug("Found completed task [" + name + "] in the tasks dictionary.");

                                    RemoveDisposeTaskFromDictionary(name);
                                }
                                else if (task.IsFaulted || task.IsCanceled)
                                {
                                    //The task errored so we will log and remove from the dictionary
                                    log.Warn("Found faulted/canceled task with name [" + name + "] in the tasks dictionary.");

                                    RemoveDisposeTaskFromDictionary(name);
                                }
                            }
                            else
                            {
                                log.Warn("Retrieved value of [" + name + "] from tasks dictionary but it was null.");

                                //Remove the task from the dictionary
                                RemoveDisposeTaskFromDictionary(name);
                            }
                        }
                        else
                        {
                            log.Warn("Attempted to check task with name [" + name + "] but failed to retrieve value. Something else is accessing dictionary.");
                        }
                    }
                    else
                    {
                        log.Error("Found key in tasks dictionary that was null."); //This shouldn't be possible
                    }
                }

                //Sleep for the configured amount of time before checking again
                Thread.Sleep(_backgroundThreadSleepTime);
            }
        }

        /// <summary>
        /// Attempts to remove the task with specific name from the dictionary. 
        /// Generates error logs if the removal fails.
        /// </summary>
        /// <param name="taskName"></param>
        private static void RemoveDisposeTaskFromDictionary(string taskName)
        {
            //Remove the key with null value from dictionary
            if (tasks.TryRemove(taskName, out Task? task))
            {
                //Check if the task is null
                if (task != null)
                {
                    //Dispose of the task
                    task.Dispose();
                }

                //Log the succesful removal
                log.Debug("Successfully removed task [" + taskName + "] from the dictionary.");
            }
            else
            {
                //Log the failure to remove value from dictionary
                log.Error("Failed to remove task named [" + taskName + "] from dictionary.");
            }
        }

        /// <summary>
        /// Shutsdown the background thread internal processes and attemps to cleanly shut down any running background threads.
        /// </summary>
        public static void Shutdown()
        {
            log.Debug("Starting background tasks shutdown.");

            //Set the shutdown flag so that new tasks are not started
            _shutdown = true;
            
            //Loop through all of the tasks in the dictionary
            foreach (string name in tasks.Keys)
            {
                //Try to remove the task from the dictionary
                if (tasks.TryRemove(name, out Task? task))
                {
                    //Check to make sure the task is not null
                    if (task != null)
                    {
                        //Dispose of the task
                        log.Debug("Disposed of task [" + name + "] during shutdown process.");

                        task.Dispose();
                    }
                }
                else
                {
                    //Log an error because we couldn't remove a task from the dictionary
                    log.Error("Failed to remove task [" + name + "] from dictionary of tasks.");
                }
            }

            log.Debug("Completed background tasks shutdown.");
        }

        /// <summary>
        /// Starts a task via the provided function. If a task with the same name has been provided already it returns false.
        /// Also if there are any errors/exceptions when starting the task it will return false. Otherwise returns true.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="function"></param>
        /// <returns></returns>
        public static bool StartBackgroundTask(string name, Func<Task?> function)
        {
            if (!_shutdown)
            {
                //Check to make sure that the name and function provided are not null
                if (name == null) { throw new ArgumentNullException("name"); }
                ValidateFunctionOrMethodNameNotNull(function);

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
                            log.Debug("Successfully added function [" + function.Method.Name + "] to dictionary with placeholder name [" + name + "]");
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
                        log.Error("Failed to add task with name [" + name + "] to the dictionary due to exception [" + ex.Message + "]");
                        return false;
                    }
                }
            }
            else
            {
                log.Warn("Attempted to start named task [" + name + "] while shutdown is in progress, ignoring.");

                //Return false because we are not starting any new tasks while shutdown is in progress
                return false;
            }
        }
        
        /// <summary>
        /// Starts a task via the provided function and returns the registered name associated with it
        /// so that it can be managed/queried later. 
        /// NOTE: The registered name is generated via Guid.NewGuid().ToString()
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public static string StartBackgroundTask(Func<Task?> function)
        {
            //Validate that function is not null and throw exception if it is
            ValidateFunctionOrMethodNameNotNull(function);

            if (!_shutdown)
            {
                //Generate a randomized name via a guid
                string name = Guid.NewGuid().ToString();

                try
                {
                    //Start the task 
                    Task task = Task.Run(function);
                    int counter = 0;

                    //Attempt to add the items to the dictionary if not try again
                    while (!tasks.TryAdd(name, task))
                    {
                        if (counter >= _numberOfAttemptsToAddToDictionary)
                        {
                            log.Warn("Failed to add function [" + function.Method.Name + "] with name [" + name + "] to the dictionary, max number of attemps reached.");

                            return "";
                        }

                        //Need to generate a new name otherwise we will loop infinitely
                        name = Guid.NewGuid().ToString();

                        counter++;

                        Thread.Sleep(_backgroundThreadSleepTime);
                    }
                }
                catch (Exception ex)
                {
                    log.Error("Exception while starting task [" + function.Method.Name + "] or adding it to the dictionary [" + ex.Message + "]");
                    throw;
                }

                log.Debug("Started background task with randomly generated name [" + name + "]");
                return name;
            }
            else
            {
                log.Warn("Attempted to start an unnamed task [" + function.Method.Name + "] while shutdown is in progress, ignoring.");

                //Return blank because shutdown is in progress and we aren't starting a new task
                return "";
            }
        }

        //TODO Should create a method that disposes of an existing background task/thread if one is created after with the same name

        /// <summary>
        /// Retrieves the status of the named task if it is present in the dictionary.
        /// If a task with the provided name was not found it will return TaskStatus.RanToCompletion
        /// </summary>
        /// <param name="taskName"></param>
        public static TaskStatus GetStatusOfTask(string taskName)
        {
            //Check if the task is present in the dictionary
            if (tasks.TryGetValue(taskName, out Task task))
            {
                //Check to make sure that the task is not null
                if (task != null)
                {
                    //Retrieve the status of the task
                    log.Debug("Retrieved task with name [" + taskName + "] with status [" + task.Status.ToString() + "]");
                    return task.Status;
                }
                else
                {
                    //The task was null
                    log.Warn("Attempted to retrieve task with name [" + taskName + "] but the returned task was null");

                    //Return TaskStatus.Faulted because this shouldn't ever happen
                    return TaskStatus.Faulted;
                }
            }
            else
            {
                //A task with the provided name was not found so it was removed from the queue or didn't exist in the first place
                log.Debug("Attempted to find task with name [" + taskName + "] but didn't find it because it didn't exist or is completed.");
                return TaskStatus.RanToCompletion;
            }
        }

        /// <summary>
        /// Checks if the named task is completed. Returns true if it has been completed, returns false otherwise.
        /// </summary>
        /// <param name="taskName"></param>
        /// <returns></returns>
        public static bool IsTaskCompleted(string taskName)
        {
            //Retrieve the status of the named task
            TaskStatus status = GetStatusOfTask(taskName);

            //Check if the status of the task is completed
            if (status == TaskStatus.RanToCompletion)
            {
                log.Debug("Retrieved status of named task [" + taskName + "] it is completed.");
                return true;
            }
            else
            {
                log.Debug("Retrieved status of named task [" + taskName + "] it is not completed.");
                return false;
            }
        }

        //Helper methods below

        /// <summary>
        /// Validates that the provided function and its Method.Name are not null. Throws
        /// ArgumentNullException if they are.
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        internal static void ValidateFunctionOrMethodNameNotNull(Func<Task?> function)
        {
            //Check for nulls
            if (function == null) { throw new ArgumentNullException("function"); }
            if (function.Method == null) { throw new ArgumentNullException("function.Method"); }
            if (function.Method.Name == null) { throw new ArgumentNullException("function.Method.Name"); }
        }


    }
}
