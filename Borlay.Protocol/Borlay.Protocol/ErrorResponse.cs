using Borlay.Serialization.Notations;
using System;
using System.Collections.Generic;
using System.Text;

namespace Borlay.Protocol
{
    [Data(20000)]
    public class ErrorResponse
    {
        [Include(0, true)]
        public ErrorCode Code { get; set; }

        [Include(1, false)]
        public string Message { get; set; } // todo peržiūrėti ar nereikia byte array
    }
}
