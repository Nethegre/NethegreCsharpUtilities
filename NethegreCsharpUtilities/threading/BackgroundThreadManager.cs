using nethegre.csharp.util.logging;
using nethegre.csharp.util.config;
using System.Collections.Concurrent;

namespace nethegre.csharp.util.threading
{
    /// <summary>
    /// A class that provides a simple interface for running threads in the background.
    /// </summary>
    public class BackgroundThreadManager
    {
        //Thread safe dictionary to store all background threads that have been started
        ConcurrentDictionary<string, Thread> threads = new ConcurrentDictionary<string, Thread>();
        ConcurrentDictionary<string, Task> tasks = new ConcurrentDictionary<string, Task>();


        //TODO need to write a generic background thread manager that supports Tasks and Action and Threads

        


    }
}
