using System;
using System.Threading.Tasks;
using MondBot.Shared;

namespace MondBot.Master
{
    static class RunModule
    {
        private static readonly WorkerManager WorkerManager = new WorkerManager();

        public static void Initialize()
        {
            // force the static constructor to run
            Console.WriteLine(WorkerManager.ToString());
        }

        public static async Task<RunResult> Run(string service, string userid, string username, string source)
        {
            if (string.IsNullOrWhiteSpace(service))
                throw new ArgumentNullException(nameof(service));

            if (string.IsNullOrWhiteSpace(userid))
                throw new ArgumentNullException(nameof(userid));

            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentNullException(nameof(username));

            if (string.IsNullOrWhiteSpace(source))
                return null;

            if (source.Length >= 5000)
                return new RunResult("ERROR: Program Too Long");

            try
            {
                // get a worker
                var worker = await WorkerManager.Get().ConfigureAwait(false);

                try
                {
                    // make it do work
                    var result = await worker.Run(service, userid, username, source).ConfigureAwait(false);

                    // reuse the worker
                    WorkerManager.Enqueue(worker);

                    return result;
                }
                catch (RunException e)
                {
                    // something went wrong, kill the worker
                    worker.Kill();

                    return new RunResult("ERROR: " + e.Message);
                }

            }
            catch (RunException e)
            {
                return new RunResult("ERROR: " + e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new RunResult("EXCEPTION: " + e.Message);
            }
        }
    }
}
