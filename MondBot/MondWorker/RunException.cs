using System;

namespace MondBot
{
    class RunException : Exception
    {
        public RunException(string message, Exception innerException = null)
            : base(message, innerException)
        {

        }
    }
}
