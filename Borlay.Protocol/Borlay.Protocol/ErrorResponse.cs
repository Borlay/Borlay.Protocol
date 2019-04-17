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
        public string Reason { get; set; }

        [Include(2, false)]
        public string Message { get; set; }

        [Include(3, false)]
        public string Field { get; set; }
    }
}
