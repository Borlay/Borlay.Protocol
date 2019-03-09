
namespace Borlay.Protocol.Converters
{
    public interface IProtocolConverter
    {
        void Apply(byte[] destination, ref int index, params DataContext[] dataContexts);
        DataContext[] Resolve(byte[] source, ref int index, int length);
        DataContext Resolve(byte[] source, ref int index);
        bool TryResolve(byte[] source, ref int index, DataFlag flag, out DataContext dataContext);
    }
}
