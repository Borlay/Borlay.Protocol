using Borlay.Serialization.Notations;
using System;
using System.Collections.Generic;
using System.Text;

namespace Borlay.Protocol
{
    [Data(20001)]
    public class EmptyResponse
    {
        [Include(0, true)]
        public bool GoneForever { get; set; }
    }
}
