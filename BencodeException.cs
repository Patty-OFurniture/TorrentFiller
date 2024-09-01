using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace HashTester
{
    [Serializable]
    public class BencodeException : Exception
    {
        public BencodeException()
        {
        }

        public BencodeException(string? message) : base(message)
        {
        }

        public BencodeException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected BencodeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}