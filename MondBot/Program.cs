using System.Threading.Tasks;
using MondBot.Master;
using MondBot.Slave;

namespace MondBot
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                await MasterProgram.Main(args);
                return;
            }

            await SlaveProgram.Main(args);
        }
    }
}
