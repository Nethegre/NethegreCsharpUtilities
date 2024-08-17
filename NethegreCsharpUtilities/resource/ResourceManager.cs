using nethegre.csharp.util.config;
using nethegre.csharp.util.logging;

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
        internal static string _resourceDirectoryPath = ConfigManager.config["resourceDirectory"];

        /// <summary>
        /// Retrieve the file based on the path provided. 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static FileStream retrieveResource(string fileName)
        {
            FileStream resourceFile = null;

            //Determine if the resource exists
            if (File.Exists(_resourceDirectoryPath + fileName))
            {
                resourceFile = File.OpenRead(_resourceDirectoryPath + fileName);
            }
            else
            {
                log.warn("Failed to find expected resource [" + fileName + "] in folder [" + _resourceDirectoryPath + "]");

                //Check to make sure that the resource directory exists
                if (Directory.Exists(_resourceDirectoryPath))
                {
                    log.error("Resource directory doesn't exist!");
                }
            }

            return resourceFile;
        }

        /// <summary>
        /// Retrieve all files in the specified folder path. 
        /// NOTE: The folder path is relative to the base running directory
        /// and doesn't have the resource directory appended to it. 
        /// </summary>
        public static List<FileStream> retrieveResources(string folderPath)
        {
            List<FileStream> files = new List<FileStream>();

            //Verify that the provided folder path is not null
            if (folderPath != null)
            {
                //Determine if the directory exists
                if (Directory.Exists(folderPath))
                {
                    //Loop through all the files in the directory
                    foreach (string filePath in Directory.EnumerateFiles(folderPath))
                    {
                        //Retrieve the FileStream for each file in the directory
                        files.Add(File.OpenRead(filePath));
                    }
                }
                else
                {
                    log.error("Directory path provided does not exist");
                }
            }
            else
            {
                log.error("The folder path provided was null");
            }

            return files;
        }

        /// <summary>
        /// Retrieves the value of the resource directory path. By default this value is pulled
        /// from the config file with the key name "resourceDirectory".
        /// </summary>
        /// <returns></returns>
        public static string getResourceFolderPath()
        {
            return _resourceDirectoryPath;
        }

        /// <summary>
        /// Sets the value of the resource directory path if the provided path exists. This will
        /// override the default value found in the config file with the key "resourceDirectory".
        /// </summary>
        /// <param name="folderPath"></param>
        /// <returns></returns>
        public static void setResourceFolderPath(string folderPath)
        {
            //Check to make sure the directory exists
            if (Directory.Exists(folderPath))
            {
                //Set the new resource folder path
                _resourceDirectoryPath = folderPath;
            }
        }
    }
}
