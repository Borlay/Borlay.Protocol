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
        IEnumerable<DataContext> SendData(IResolverSession session, DataInjectContext dataInjectContext);
        void ReceiveData(IResolverSession session, DataInjectContext dataInjectContext);
    }

    public class ActionDataInject : IDataInject
    {
        Func<IResolverSession, DataInjectContext, IEnumerable<DataContext>> sendData;
        Action<IResolverSession, DataInjectContext> receiveData;

        public ActionDataInject(Func<IResolverSession, DataInjectContext, IEnumerable<DataContext>> sendData, Action<IResolverSession, DataInjectContext> receiveData)
        {
            this.sendData = sendData;
            this.receiveData = receiveData;
        }

        public void ReceiveData(IResolverSession session, DataInjectContext dataInjectContext)
        {
            receiveData?.Invoke(session, dataInjectContext);
        }

        public IEnumerable<DataContext> SendData(IResolverSession session, DataInjectContext dataInjectContext)
        {
            return sendData?.Invoke(session, dataInjectContext);
        }
    }

    public class DataInjectContext
    {
        public ConverterHeader ConverterHeader { get; set; }

        public RequestHeader RequestHeader { get; internal set; }

        public Func<ILookup<DataFlag, DataContext>> GetData { get; internal set; }
    }
}
