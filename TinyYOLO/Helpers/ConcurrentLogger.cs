using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TinyYOLO.Helpers
{
    public static class ConcurrentLogger
    {
        private static readonly SemaphoreSlim PrintMutex = new SemaphoreSlim(1);
        private static readonly BlockingCollection<string> MessageQueue = new BlockingCollection<string>();

        public static void WriteLine(string message)
        {
            var timestamp = DateTime.Now;

            // Push the message on the queue
            MessageQueue.Add(timestamp.ToString("o") + ": " + message);

            // Start a new task that will dequeue one message and print it. The tasks will not
            // necessarily run in order, but since each task just takes the oldest message and
            // prints it, the messages will print in order. 
            Task.Run(async () =>
            {
                // Wait to get access to the queue. 
                await PrintMutex.WaitAsync();

                try
                {
                    string msg = MessageQueue.Take();
                    Debug.WriteLine(msg);
                }
                finally
                {
                    PrintMutex.Release();
                }
            });
        }
    }
}
