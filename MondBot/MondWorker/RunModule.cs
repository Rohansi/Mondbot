using System;
using System.Threading.Tasks;

namespace MondBot
{
    static class RunModule
    {
        private static readonly WorkerManager WorkerManager = new WorkerManager();

        public static async Task<string> Run(string username, string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return null;

            if (source.Length >= 5000)
                return "ERROR: Program Too Long";

            try
            {
                // get a worker
                var worker = await WorkerManager.Get().ConfigureAwait(false);

                try
                {
                    // make it do work
                    var result = await worker.Run(username, source).ConfigureAwait(false);

                    // reuse the worker
                    WorkerManager.Enqueue(worker);

                    return result;
                }
                catch (RunException e)
                {
                    // something went wrong, kill the worker
                    worker.Kill();

                    return "ERROR: " + e.Message;
                }

            }
            catch (RunException e)
            {
                return "ERROR: " + e.Message;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return "EXCEPTION: " + e.Message;
            }
        }
    }
}
