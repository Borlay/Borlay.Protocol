using System;
using System.Collections.Generic;
using System.Text;

namespace Borlay.Protocol
{
    public class EmptyResponseException : ProtocolException // todo iškelti
    {
        public EmptyResponseException()
            : base(ErrorCode.DataNotFound)
        {

        }

        public EmptyResponseException(string message)
            : base(message, ErrorCode.DataNotFound)
        {
        }

        public EmptyResponseException(string message, Exception innerException)
            : base(message, innerException, ErrorCode.DataNotFound)
        {
        }
    }
}
