using Borlay.Handling;
using System.Threading;
using System.Threading.Tasks;

namespace Borlay.Protocol
{
    public interface IRequestAsync
    {
        Task<T> SendRequestAsync<T>(IActionMeta actionSolve, object obj, bool throwOnEmpty, CancellationToken cancellationToken);
        Task<object> SendRequestAsync(IActionMeta actionSolve, object obj, bool throwOnEmpty, CancellationToken cancellationToken);
    }
}
