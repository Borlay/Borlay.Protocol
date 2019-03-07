using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Borlay.Protocol
{
    public class PacketStream : IPacketStream
    {
        public Stream Stream { get; }

        public PacketStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            this.Stream = stream;
        }

        public virtual void WritePacket(byte[] bytes, int count, bool addCount)
        {
            if (count > ushort.MaxValue)
                throw new ArgumentException(nameof(count));

            Stream.WritePacket(bytes, (ushort)count, addCount);
        }

        public virtual Task WritePacketAsync(byte[] bytes, int count, bool addCount, CancellationToken cancellationToken)
        {
            if (count > ushort.MaxValue)
                throw new ArgumentException(nameof(count));

            return Stream.WritePacketAsync(bytes, (ushort)count, addCount, cancellationToken);
        }

        public virtual Task<int> ReadPacketAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            return Stream.ReadPacketAsync(buffer, cancellationToken);
        }

        public virtual int ReadPacket(byte[] buffer, CancellationToken cancellationToken)
        {
            return Stream.ReadPacket(buffer, cancellationToken);
        }
    }

    public interface IPacketStream
    {
        void WritePacket(byte[] bytes, int count, bool addCount);
        int ReadPacket(byte[] buffer, CancellationToken cancellationToken);

        Task WritePacketAsync(byte[] bytes, int count, bool addCount, CancellationToken cancellationToken);
        Task<int> ReadPacketAsync(byte[] buffer, CancellationToken cancellationToken);
    }
}
