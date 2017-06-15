using MondBot.Master;
using MondBot.Slave;

namespace MondBot
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                MasterProgram.Main(args);
                return;
            }

            SlaveProgram.Main(args);
        }
    }
}
