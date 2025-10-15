using nethegre.csharp.util.config;
using nethegre.csharp.util.logging;
using System.Collections.ObjectModel;

namespace nethegre.csharp.util.resource
{
    /// <summary>
    /// A class that provides an easy interface for reading external resource
    /// files.
    /// 
    /// TODO Add functionality that supports the retrieval of resource files from multiple directories
    /// </summary>
    public static class ResourceManager
    {
        //Create an instance of the log manager class
        readonly static LogManager log = new LogManager(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        //Stores the resource directory path that is currently used
        internal static string _resourceDirectoryPathPrefix = ConfigManager.config["resourceDirectoryPrefix"] ?? "resources/";

        /// <summary>
        /// Retrieve the file based on the path provided. Will return null FileStream if the file didn't exist.
        /// NOTE: If the directoryPrefix is not used the path will need to start relative to the working directory or be an absolute path.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="useDirectoryPrefix"></param>
        /// <returns></returns>
        public static FileStream? RetrieveResource(string filePath, bool useDirectoryPrefix = false)
        {
            FileStream? resourceFile = null;

            //Check if we are using a directoryPrefix
            if (useDirectoryPrefix)
            {
                log.Debug("Using the directoryPrefix");

                //Determine if the resource exists
                if (File.Exists(_resourceDirectoryPathPrefix + filePath))
                {
                    resourceFile = File.OpenRead(_resourceDirectoryPathPrefix + filePath);
                    log.Debug("Opened the file at the provided path with directory prefix.");
                }
                else
                {
                    log.Warn("Failed to find expected resource [" + filePath + "] in folder [" + _resourceDirectoryPathPrefix + "]");

                    //Check to make sure that the resource directory exists
                    if (Directory.Exists(_resourceDirectoryPathPrefix))
                    {
                        log.Error("Resource directory doesn't exist!");
                    }
                }
            }
            else
            {
                //Determine if the resource exists
                if (File.Exists(filePath))
                {
                    resourceFile = File.OpenRead(filePath);
                    log.Debug("Opened the file at the provided path.");
                }
                else
                {
                    log.Warn("Failed to find expected resource at the path [" + filePath + "]");
                }
            }

            return resourceFile;
        }

        /// <summary>
        /// Returns all of the files in the directory path. Will return empty collection if the path is invalid.
        /// NOTE: If the directoryPrefix is not used the path will need to start relative to the working directory or be an absolute path.
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="useDirectoryPrefix"></param>
        /// <returns></returns>
        public static Collection<FileStream> RetrieveResources(string folderPath, bool useDirectoryPrefix = false)
        {
            Collection<FileStream> files = new Collection<FileStream>();

            //Verify that the provided folder path is not null
            if (folderPath != null)
            {
                //Check if we are using the directoryPrefix
                if (useDirectoryPrefix)
                {
                    log.Debug("Using the directoryPrefix");

                    //Determine if the directory exists
                    if (Directory.Exists(_resourceDirectoryPathPrefix + folderPath))
                    {
                        //Loop through all the files in the directory
                        foreach (string filePath in Directory.EnumerateFiles(_resourceDirectoryPathPrefix + folderPath))
                        {
                            try
                            {
                                //Retrieve the FileStream for each file in the directory
                                files.Add(File.OpenRead(filePath));
                                log.Debug("Added file [" + filePath + "] to the collection.");
                            }
                            catch (Exception ex)
                            {
                                log.Error("Failed to read the file at file path [" + filePath + "]");
                            }
                        }
                    }
                    else
                    {
                        log.Error("Directory path provided does not exist");
                    }
                }
                else
                {
                    //Determine if the directory exists
                    if (Directory.Exists(folderPath))
                    {
                        //Loop through all the files in the directory
                        foreach (string filePath in Directory.EnumerateFiles(folderPath))
                        {
                            try
                            {
                                //Retrieve the FileStream for each file in the directory
                                files.Add(File.OpenRead(filePath));
                                log.Debug("Added file [" + filePath + "] to the collection.");
                            }
                            catch (Exception ex)
                            {
                                log.Error("Failed to read the file at file path [" + filePath + "]");
                            }
                        }
                    }
                    else
                    {
                        log.Error("Directory path provided does not exist");
                    }
                }
            }
            else
            {
                log.Error("The folder path provided was null");
            }

            return files;
        }

        /// <summary>
        /// Retrieves the value of the resource directory path. By default this value is pulled
        /// from the config file with the key name "resourceDirectory".
        /// </summary>
        /// <returns></returns>
        public static string GetResourceDirectoryPathPrefix()
        {
            return _resourceDirectoryPathPrefix;
        }

        /// <summary>
        /// Sets the value of the resource directory path prefix if the provided path exists. This will
        /// override the default value found in the config file with the key "resourceDirectory".
        /// </summary>
        /// <param name="directoryPrefix"></param>
        /// <returns></returns>
        public static void SetResourcePathPrefix(string directoryPrefix)
        {
            //Check to make sure the directory exists
            if (Directory.Exists(directoryPrefix))
            {
                //Set the new resource folder path
                _resourceDirectoryPathPrefix = directoryPrefix;
            }
        }
    }
}
