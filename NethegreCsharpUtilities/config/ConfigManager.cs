using Microsoft.Extensions.Configuration;

namespace nethegre.csharp.util.config
{
    public class ConfigManager
    {
        //NOTE: This class is a dependent of the logging class so it can't initialize the logging class otherwise there will be a dependency loop

        //Configure the config json file
        public static IConfiguration config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).Build();

        //Helper methods below
        public static string[] getConfigList(string configSectionName)
        {
            string[] configList = new string[0];

            try
            {
                //Retreive the config section by the name provided
                IConfigurationSection configSection = config.GetSection(configSectionName);

                //Thank you to https://stackoverflow.com/questions/41329108/asp-net-core-get-json-array-using-iconfiguration for helping me figure this out
                configList = configSection.GetChildren().ToArray().Select(c => c.Value).ToArray();

            }
            catch (Exception ex)
            {
                Console.WriteLine("[ConfigManager.getConfigList] ERROR - Exception while attempting to retreive config section [" + configSectionName + "]: Ex. [" + ex.Message + "]");
            }

            return configList;
        }
    }
}
