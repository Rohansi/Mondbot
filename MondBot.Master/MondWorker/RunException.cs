using System;

namespace MondBot.Master
{
    class RunException : Exception
    {
        public RunException(string message, Exception innerException = null)
            : base(message, innerException)
        {

        }
    }
}
