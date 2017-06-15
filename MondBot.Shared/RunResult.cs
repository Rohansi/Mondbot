namespace MondBot.Shared
{
    public class RunResult
    {
        public string Output { get; }
        public byte[] Image { get; }

        public RunResult(string output, byte[] image = null)
        {
            Output = output;
            Image = image ?? new byte[0];
        }
    }
}
