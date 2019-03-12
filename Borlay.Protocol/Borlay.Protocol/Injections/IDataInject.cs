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
        IEnumerable<DataContext> SendData(IResolver resolver, DataInjectContext dataInjectContext);
        Resolver ReceiveData(IResolver resolver, DataInjectContext dataInjectContext);
    }

    public class ActionDataInject : IDataInject
    {
        Func<IResolver, DataInjectContext, IEnumerable<DataContext>> sendData;
        Func<IResolver, DataInjectContext, Resolver> receiveData;

        public ActionDataInject(Func<IResolver, DataInjectContext, IEnumerable<DataContext>> sendData, Func<IResolver, DataInjectContext, Resolver> receiveData)
        {
            this.sendData = sendData;
            this.receiveData = receiveData;
        }

        public Resolver ReceiveData(IResolver resolver, DataInjectContext dataInjectContext)
        {
            return receiveData?.Invoke(resolver, dataInjectContext);
        }

        public IEnumerable<DataContext> SendData(IResolver resolver, DataInjectContext dataInjectContext)
        {
            return sendData?.Invoke(resolver, dataInjectContext);
        }
    }

    public class DataInjectContext
    {
        public ConverterHeader ConverterHeader { get; set; }

        public RequestHeader RequestHeader { get; internal set; }

        public Func<ILookup<DataFlag, DataContext>> GetData { get; internal set; }
    }
}
