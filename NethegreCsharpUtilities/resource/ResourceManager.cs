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
        internal static string _resourceDirectoryPath = ConfigManager.config["resourceDirectory"] ?? "resources/";

        /// <summary>
        /// Retrieve the file based on the path provided. Will return null FileStream if the file didn't exist.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static FileStream RetrieveResource(string fileName)
        {
            FileStream resourceFile = null;

            //Determine if the resource exists
            if (File.Exists(_resourceDirectoryPath + fileName))
            {
                resourceFile = File.OpenRead(_resourceDirectoryPath + fileName);
            }
            else
            {
                log.Warn("Failed to find expected resource [" + fileName + "] in folder [" + _resourceDirectoryPath + "]");

                //Check to make sure that the resource directory exists
                if (Directory.Exists(_resourceDirectoryPath))
                {
                    log.Error("Resource directory doesn't exist!");
                }
            }

            return resourceFile;
        }

        /// <summary>
        /// Returns all of the files in the directory path.
        /// NOTE: The directory path starts at the root directory of the project.
        /// </summary>
        /// <param name="folderPath"></param>
        /// <returns></returns>
        public static List<FileStream> RetrieveResources(string folderPath)
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
                        try
                        {
                            //Retrieve the FileStream for each file in the directory
                            files.Add(File.OpenRead(filePath));
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
                log.Error("The folder path provided was null");
            }

            return files;
        }

        /// <summary>
        /// Retrieves the value of the resource directory path. By default this value is pulled
        /// from the config file with the key name "resourceDirectory".
        /// </summary>
        /// <returns></returns>
        public static string GetResourceFolderPath()
        {
            return _resourceDirectoryPath;
        }

        /// <summary>
        /// Sets the value of the resource directory path if the provided path exists. This will
        /// override the default value found in the config file with the key "resourceDirectory".
        /// </summary>
        /// <param name="folderPath"></param>
        /// <returns></returns>
        public static void SetResourceFolderPath(string folderPath)
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
