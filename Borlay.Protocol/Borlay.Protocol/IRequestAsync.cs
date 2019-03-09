using Borlay.Protocol.Converters;
using System.Threading;
using System.Threading.Tasks;

namespace Borlay.Protocol
{
    public interface IRequestAsync
    {
        //Task<T> SendRequestAsync<T>(IActionMeta actionMeta, object obj, bool throwOnEmpty, CancellationToken cancellationToken);
        Task<object> SendRequestAsync(DataContext[] argumentContexts, CancellationToken cancellationToken);
    }
}
