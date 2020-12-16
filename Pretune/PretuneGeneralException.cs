using System;
using System.Runtime.Serialization;

namespace Pretune
{
    [Serializable]
    class PretuneGeneralException : Exception
    {
        public PretuneGeneralException()
        {
        }

        public PretuneGeneralException(string? message) : base(message)
        {
        }

        public PretuneGeneralException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected PretuneGeneralException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}