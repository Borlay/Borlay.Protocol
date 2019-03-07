﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Borlay.Protocol
{
    public class ConnectionException : Exception
    {
        public ConnectionError Error { get; set; }

        public ConnectionException(ConnectionError connectionError)
            : base($"Connection error occurred: '{connectionError}'")
        {
            this.Error = connectionError;
        }

        public ConnectionException(string message, ConnectionError connectionError)
            : base(message)
        {
            this.Error = connectionError;
        }

        public ConnectionException(string message, Exception innerException, ConnectionError connectionError)
            : base(message, innerException)
        {
            this.Error = connectionError;
        }
    }

    public enum ConnectionError
    {
        Unknown = 0,
        AlreadyConnected = 1,
        AlreadyListening = 2,
        Disconnected = 3,
        NotConnected = 4,
        Rejected = 5,
        BadHandshake = 6,
        FlagNotSupported = 7,
        BadResponse = 8,
        NotReady = 9,
    }
}
