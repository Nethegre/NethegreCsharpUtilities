using Microsoft.Extensions.Configuration;

namespace nethegre.csharp.util.config
{
    /// <summary>
    /// A class that provides an easy interface into a configuration file. 
    /// The default configuration file should be named "config.json" and 
    /// should be placed in the same directory as the utils .dll file.
    /// The configuration file used can be overriden via the 
    /// 
    /// Implementation:
    /// A typical implementation example is as follows "nethegre.csharp.util.config.ConfigManager.config['config name'];"
    ///     The static "config" IConfiguration object can be used as a dictionary 
    ///     where the key name is the literal string value that corresponds to the
    ///     key within the config file.
    ///     
    /// Another implementation that can be used is the "getConfigList" method which 
    ///     returns an array of strings for the key provided.
    /// </summary>
    public static class ConfigManager
    {
        //NOTE: This class is a dependent of the logging class so it can't initialize the logging class otherwise there will be a dependency loop

        //Configure the config json file
        public static IConfiguration config = new ConfigurationBuilder().AddJsonFile("config.json", optional: false, reloadOnChange: true).Build();

        //Helper methods below

        /// <summary>
        /// Returns a list of strings for the provided config section name. 
        /// Will return an empty list of the section doesn't exist 
        /// </summary>
        /// <param name="configSectionName"></param>
        /// <returns>The list of string items based on the config section name.</returns>
        public static List<T> getConfigList<T>(string configSectionName, bool returnEmptyOnFail = true)
        {
            List<T> configList = new List<T>();

            try
            {
                //Retreive the config section by the name provided
                IConfigurationSection configSection = config.GetSection(configSectionName);

                //Thank you to https://stackoverflow.com/questions/41329108/asp-net-core-get-json-array-using-iconfiguration for helping me figure this out
                //Retrieve the array of strings from the config section if possible
                string[] configItems = configSection.GetChildren().ToArray().Select(c => c.Value).ToArray();

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
                                    return new List<T>();
                                }
                            }
                            else
                            {
                                throw ex;
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
        /// Sets the new class level config file.
        /// </summary>
        /// <param name="filePath"></param>
        public static void setConfigFilePath(string filePath)
        {
            //Check to make sure that the filePath exists
            if (File.Exists(filePath))
            {
                config = new ConfigurationBuilder().AddJsonFile(filePath, optional: false, reloadOnChange: true).Build();
            }
            else
            {
                Console.WriteLine("[ConfigManager.setConfigFilePath] ERROR - Failed to find new config file [" + filePath + "]");
            }
        }

        /// <summary>
        /// Adds a list of files to the config path if they are valid.
        /// </summary>
        /// <param name="filePaths"></param>
        public static void addConfigFiles(string[] filePaths)
        {
            //Loop through each filePath and verify that they exist
            List<string> validPaths = new List<string>();

            foreach (string path in filePaths)
            {
                //Check to make sure that the filePath exists
                if (File.Exists(path))
                {
                    validPaths.Add(path);
                }
                else
                {
                    Console.WriteLine("[ConfigManager.addConfigFiles] ERROR - Failed to find new config file [" + path + "]");
                }
            }

            //Add the valid paths to the config item
            ConfigurationBuilder builder = new ConfigurationBuilder();

            foreach (string path in validPaths)
            {
                //Add the new file path to the builder
                builder.AddJsonFile(path, optional: false, reloadOnChange: true);
            }

            //Add the default config file to the builder
            builder.AddJsonFile("config.json", optional: false, reloadOnChange: true);

            //Build the builder and add to the config item
            config = builder.Build();
        }

    }
}
