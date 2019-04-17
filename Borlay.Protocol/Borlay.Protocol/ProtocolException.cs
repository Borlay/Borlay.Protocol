using System;
using System.Collections.Generic;
using System.Text;

namespace Borlay.Protocol
{
    public class ProtocolException : ConnectionException
    {
        public ErrorCode ResponseError { get; set; }

        public ProtocolException(ErrorCode responseError)
            : base(ConnectionError.BadResponse)
        {
            this.ResponseError = responseError;
        }

        public ProtocolException(string message, ErrorCode responseError)
            : base(message, ConnectionError.BadResponse)
        {
            this.ResponseError = responseError;
        }

        public ProtocolException(string message, Exception innerException, ErrorCode responseError)
            : base(message, innerException, ConnectionError.BadResponse)
        {
            this.ResponseError = responseError;
        }
    }

    public class ResponseProtocolException : ProtocolException, IResponseException
    {
        public ErrorResponse Response { get; private set; }

        public ResponseProtocolException(ErrorResponse errorResponse)
            : base(errorResponse.Message, errorResponse.Code)
        {
            this.Response = Response;
        }
    }

    public interface IResponseException
    {
        ErrorResponse Response { get; }
    }

    public enum ErrorCode
    {
        UnknownType = 1,
        UnknownRequest = 2,
        UnknownResponse = 3,
        VersionNotSupported = 4,
        Unauthorized = 5,
        DataNotValid = 6,
        DataNotFound = 7,
        BadSerializer = 8,
        BadRequest = 9,
        HandlerNotFound = 10,
        TooManyRequests = 11, // todo throw 
        Closed = 12
    }
}
