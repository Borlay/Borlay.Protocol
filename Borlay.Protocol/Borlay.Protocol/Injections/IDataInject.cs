using Borlay.Injection;
using Borlay.Protocol.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Borlay.Protocol.Injections
{
    public interface IDataInject
    {
        IEnumerable<DataContext> SendData(DataInjectContext contex);
        Resolver ReceiveData(DataInjectContext context);
    }

    public class DataInjectContext
    {
        public ConverterHeader ConverterHeader { get; set; }

        public RequestHeader RequestHeader { get; internal set; }

        public Func<ILookup<DataFlag, DataContext>> GetData { get; internal set; }
    }
}
