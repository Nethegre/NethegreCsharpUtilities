using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.AccessControl;
using static nethegre.csharp.util.logging.LogManager;

namespace nethegre.csharp.util.config
{
    /// <summary>
    /// A class that provides an easy interface into a configuration file. 
    /// The default configuration file should be named "config.json" and 
    /// should be placed in the same directory as the utils .dll file.
    /// The configuration file used can be overriden via the <see cref="setDefaultConfigFile(string)"/> or 
    /// the <see cref="addConfigFiles"/> methods which can be used to replace the default config
    /// file path or add other config files for the project.
    /// <para>
    /// Implementation:
    /// A typical implementation example is as follows <c>nethegre.csharp.util.config.ConfigManager.config['item'];</c>
    /// where the 'item' is the name of the configuration key for the item to retrieve.
    /// </para>
    /// <para>
    /// Another implementation that can be used is the <see cref="getConfigList"/> method which 
    /// returns an array of T for the key and type provided.
    /// </para>
    /// </summary>
    public static class ConfigManager
    {
        //NOTE: This class is a dependent of the logging class so it can't initialize the logging class otherwise there will be a dependency loop

        /// <summary>
        /// The default instance of the config file. Defaults to using "config.json" if not otherwise changed.
        /// The default config file can be changed via the setDefaultConfigFile method.
        /// </summary>
        public static IConfiguration config { 
            get 
            {
                //Need to lock the _backgroundProcessing variable so that another thread/instance doesn't attempt to populate it at the same time
                lock (_backgroundProcessing)
                {
                    //Check if the _backgroundProcessing is null
                    if (_backgroundProcessing == null) _backgroundProcessing = Task.Run(checkForExternalNestedConfigFiles);
                }

                return _config; 
            } 
        }

        /// <summary>
        /// Controls when a config file is not found if it is removed from the config file list or not. 
        /// </summary>
        internal static bool removeConfigFileIfNotFound = true;

        /// <summary>
        /// The key used within a config file to declare any defined, nested config files that should
        /// be added to the config manager.
        /// </summary>
        public const string nestedConfigFileKey = "nestedConfig";

        //The actual functional IConfiguration implementation hidden from the public by the readonly "config" item
        internal static IConfiguration _config = addDefaultConfigFile().Build();

        //Save the additional file paths to this static list so that every time we add more we can use the old ones too
        internal static List<string> _configurationFilePaths = new List<string>();

        //This will be overriden if the user decides to change the default config file.
        internal static string _defaultConfigFile = "config.json";

        //Used to track if the log processing has been started or not
        private static Task _backgroundProcessing = null;

        //Used to stop background processing
        private static bool _shutdown = false;

        #region publicMethods

        /// <summary>
        /// Returns a list of strings for the provided config section name. 
        /// Will return an empty list of the section doesn't exist 
        /// </summary>
        /// <param name="configSectionName"></param>
        /// <returns>The list of string items based on the config section name.</returns>
        /// TODO Not sure if the returnEmptyOnFail parameter actually changes behaviour because I think it always does this
        public static Collection<T> getConfigList<T>(string configSectionName, bool returnEmptyOnFail = true)
        {
            Collection<T> configList = new Collection<T>();

            try
            {
                //Thank you to https://stackoverflow.com/questions/41329108/asp-net-core-get-json-array-using-iconfiguration for helping me figure this out
                //Retrieve the array of strings from the config section if possible
                string[] configItems = config.GetSection(configSectionName).GetChildren().ToArray().Select(c => c.Value).ToArray();

                //Loop through the items and convert them into the desired type and add them to the returned list
                foreach (string item in configItems)
                {
                    //Check to make sure that the item is not null or empty before attempting to convert it
                    if (!string.IsNullOrEmpty(item))
                    {
                        try
                        {
                            //Add the item to the list if the conversion as successful
                            configList.Add((T)Convert.ChangeType(item, typeof(T)));
                        }
                        catch (Exception ex)
                        {
                            if (ex is System.InvalidCastException || ex is System.FormatException || ex is System.OverflowException)
                            {
                                Console.WriteLine("[ConfigManager.getConfigList] ERROR - Exception while attempting to convert item [" + item + "] in config section [" + configSectionName + "]: Ex. [" + ex.Message + "]");

                                //Return an empty list if the user calls it with that option
                                if (returnEmptyOnFail)
                                {
                                    return new Collection<T>();
                                }
                            }
                            else
                            {
                                throw;
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("[ConfigManager.getConfigList] WARN - Skipping config section item that is null in config section [" + configSectionName + "]");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ConfigManager.getConfigList] ERROR - Exception while attempting to retreive config section [" + configSectionName + "]: Ex. [" + ex.Message + "]");
            }

            return configList;
        }

        /// <summary>
        /// Adds a list of files to the config path if they are valid.
        /// </summary>
        /// <param name="filePaths"></param>
        public static void addConfigFiles(string[] filePaths)
        {
            //Validate that filePaths is not null
            if (filePaths != null)
            {
                //Loop through each filePath and verify that they exist
                foreach (string path in filePaths)
                {
                    //Check to make sure that the filePath exists
                    if (File.Exists(path))
                    {
                        //Check if the list of valid config files alread has this item
                        if (!_configurationFilePaths.Contains(path))
                        {
                            _configurationFilePaths.Add(path);
                        }
                    }
                    else
                    {
                        Console.WriteLine("[ConfigManager.addConfigFiles] ERROR - Failed to find new config file " +
                            "[" + path + "] not adding to the list of config files.");
                    }
                }

                rebuildConfigurationBuilder();
            }
            else
            {
                Console.WriteLine("[ConfigManager.addConfigFiles] ERROR - The provided list of config files was null.");
            }
        }

        /// <summary>
        /// Overrides the default config file if the provided file path is valid.
        /// </summary>
        /// <param name="filePath"></param>
        public static void setDefaultConfigFile(string filePath)
        {
            //Check to make sure the file exists
            if (File.Exists(filePath))
            {
                _defaultConfigFile = filePath;

                //Remake the ConfigurationBuilder with the new default
                rebuildConfigurationBuilder();
            }
            else
            {
                Console.WriteLine("[ConfigManager.setDefaultConfigFile] ERROR - Failed to find new config file [" + filePath + "]");
            }
        }

        /// <summary>
        /// Prompts the configuration manager class to detect and add any config
        /// files that are defined in any registered config files via the constant
        /// <see cref="nestedConfigFileKey"/> key value. 
        /// </summary>
        public static void updateNestedConfigFiles()
        {
            try
            {
                //TODO Should consider supporting a single field config line for this
                //This is my attmept to verify that the config section exists
                if (config.GetChildren().Any(item => item.Key == nestedConfigFileKey))
                {
                    //ConfigurationSection configSection = (ConfigurationSection)config.GetSection(nestedConfigFileKey);

                    //configSection.Exists();

                    //Check to see if the nestedConfigFileKey exists within the registered config
                    Collection<string> nestedConfigFiles = getConfigList<string>(nestedConfigFileKey, true);

                    addConfigFiles(nestedConfigFiles.ToArray());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ConfigManager.updateNestedConfigFiles] ERROR - Exception attempting to update" +
                    "the nested configuration files [" + ex.Message + "]");
            }
        }

        /// <summary>
        /// Stops background updating of config files
        /// </summary>
        public static void shutdown()
        {
            //There is no need for us to wait before setting the shutdown flag as compared to the log writer
            _shutdown = true;
        }

        #endregion publicMethods

        #region privateMethods

        /// <summary>
        /// Adds the default configuration file to the provided ConfigurationBuilder
        /// </summary>
        /// <param name="builder"></param>
        internal static void addDefaultConfigFile(ConfigurationBuilder builder)
        {
            builder.AddJsonFile(_defaultConfigFile, optional: false, reloadOnChange: true);
        }

        /// <summary>
        /// Adds the default configuration file to the returned ConfigurationBuilder
        /// </summary>
        /// <returns></returns>
        internal static ConfigurationBuilder addDefaultConfigFile()
        {
            return (ConfigurationBuilder)new ConfigurationBuilder().AddJsonFile(_defaultConfigFile, optional: false, reloadOnChange: true);
        }

        /// <summary>
        /// Builds a new ConfigurationBuilder and replaces the public static one attached to this class based on
        /// the default and additional config files set by end user.
        /// </summary>
        internal static void rebuildConfigurationBuilder()
        {
            //Add the default config file
            ConfigurationBuilder builder = addDefaultConfigFile();

            //Add each of the user added config files
            foreach (string filePath in _configurationFilePaths)
            {
                //Check if the file exists because it could have been removed since it was last added
                if (File.Exists(filePath))
                {
                    //Add to the builder
                    builder.AddJsonFile(filePath, optional: false, reloadOnChange: true);
                }
                else if (removeConfigFileIfNotFound)
                {
                    //Remove the file because the remove config file if not found option was set to true
                    _configurationFilePaths.Remove(filePath);
                }
                else
                {
                    Console.WriteLine("[ConfigManager.rebuildConfigurationBuilder] WARN - Failed to find config file " +
                        "[" + filePath + "] not adding it to the config builder.");
                }
            }

            //Dispose the previous ConfigurationBuilder before replacing it
            ConfigurationRoot configRoot = (ConfigurationRoot)_config;
            configRoot.Dispose();

            //Replace the previous ConfigurationBuilder with the new one
            _config = builder.Build();
        }

        /// <summary>
        /// Method that is used to periodically check if there has been a nested config file added
        /// will stop processing if <see cref="_shutdown"/> is true. 
        /// </summary>
        /// <returns></returns>
        internal static async Task checkForExternalNestedConfigFiles()
        {
            //TODO Maybe have this be a lambda trigger based on a config file changing

            while (!_shutdown)
            {
                //Check for nested config files
                updateNestedConfigFiles();

                //Wait 1 second before checking the config files again
                // will maybe consider making this take longer
                Thread.Sleep(1000);
            }

            //Need to lock the _backgroundProcessing variable so we can dispose of it cleanly
            lock (_backgroundProcessing)
            {
                //Dispose of the background thread on shutdown
                _backgroundProcessing.Dispose();
                _backgroundProcessing = null; //Setting this to null will allow it to be setup again.
            }
        }

        #endregion privateMethods
    }
}
