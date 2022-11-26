using nethegre.csharp.util.config;
using nethegre.csharp.util.logging;

namespace nethegre.csharp.util.resource
{
    public class ResourceManager
    {
        //Create an instance of the log manager class
        readonly static LogManager log = new LogManager(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        //Retrieve the file based on the name provided
        public static FileStream retrieveResource(string fileName)
        {
            FileStream resourceFile = null;
            string resourceFolder = ConfigManager.config["resourceFolder"];

            //Determine if the resource exists
            if (File.Exists(resourceFolder + fileName))
            {
                resourceFile = File.OpenRead(resourceFolder + fileName);
            }
            else
            {
                log.warn("Failed to find expected resource [" + fileName + "] in folder [" + resourceFolder + "]");
            }

            return resourceFile;
        }
    }
}
