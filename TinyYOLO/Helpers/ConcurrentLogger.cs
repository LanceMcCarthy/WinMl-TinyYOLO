using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TinyYOLO.Helpers
{
    public static class ConcurrentLogger
    {
        private static readonly SemaphoreSlim printMutex = new SemaphoreSlim(1);
        private static readonly BlockingCollection<string> messageQueue = new BlockingCollection<string>();

        public static void WriteLine(string message)
        {
            var timestamp = DateTime.Now;

            // Push the message on the queue
            messageQueue.Add(timestamp.ToString("o") + ": " + message);

            // Start a new task that will dequeue one message and print it. The tasks will not
            // necessarily run in order, but since each task just takes the oldest message and
            // prints it, the messages will print in order. 
            Task.Run(async () =>
            {
                // Wait to get access to the queue. 
                await printMutex.WaitAsync();

                try
                {
                    string msg = messageQueue.Take();
                    Debug.WriteLine(msg);
                }
                finally
                {
                    printMutex.Release();
                }
            });
        }
    }
}
