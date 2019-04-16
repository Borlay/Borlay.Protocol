using Borlay.Arrays;
using Borlay.Handling;
using Borlay.Injection;
using Borlay.Protocol.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Borlay.Protocol
{
    public interface IProtocolHandler
    {
        Task<DataContent> HandleDataAsync(IResolverSession session, DataContent dataContent, CancellationToken cancellationToken);
    }

    // todo repositories:
    // Borlay.Protocol.Handling

    // Borlay.Protocol.Interfaces
    // Borlay.Protocol.Methods
    // Borlay.Handling.Interfaces
    // Borlay.Handling.Methods

    // Borlay.Protocol.Host

    public class DataContent : IEnumerable<DataContext>, ILookup<DataFlag, DataContext>
    {
        protected readonly ILookup<DataFlag, DataContext> contextLookup;

        public DataContext[] DataContexts { get; private set; }

        public int Count => contextLookup.Count;

        public DataContent(params DataContext[] contexts)
        {
            contextLookup = contexts.ToLookup(c => c.DataFlag);
            DataContexts = contexts;
        }

        public virtual IEnumerable<DataContext> this[DataFlag flag]
        {
            get
            {
                return contextLookup[flag];
            }
        }

        public IEnumerator<DataContext> GetEnumerator()
        {
            return DataContexts.AsEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return DataContexts.GetEnumerator();
        }

        public bool Contains(DataFlag key)
        {
            return contextLookup.Contains(key);
        }

        IEnumerator<IGrouping<DataFlag, DataContext>> IEnumerable<IGrouping<DataFlag, DataContext>>.GetEnumerator()
        {
            return contextLookup.GetEnumerator();
        }
    }
}
