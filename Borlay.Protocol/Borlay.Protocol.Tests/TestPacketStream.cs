using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Borlay.Protocol.Tests
{
    public class TestPacketStream : IPacketStream
    {
        private readonly Queue<byte[]> queue = new Queue<byte[]>();
        System.Threading.EventWaitHandle ewh = new EventWaitHandle(true, EventResetMode.AutoReset);

        private TestPacketStream packetStream;

        public void Connected(TestPacketStream packetStream)
        {
            this.packetStream = packetStream;
        }

        public void Append(byte[] bytes)
        {
            this.queue.Enqueue(bytes);
            ewh.Set();
        }

        public int ReadPacket(byte[] buffer, CancellationToken cancellationToken)
        {
            do
            {
                if (this.queue.Count > 0)
                {
                    var bytes = this.queue.Dequeue();
                    Array.Copy(bytes, buffer, bytes.Length);
                    return bytes.Length;
                }
                ewh.WaitOne();
            } while (!cancellationToken.IsCancellationRequested);
            return 0;
        }

        public Task<int> ReadPacketAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public void WritePacket(byte[] bytes, int count, bool addCount)
        {
            var buffer = new byte[count - 4];
            Array.Copy(bytes, 4, buffer, 0, count - 4);
            packetStream.Append(buffer);
        }

        public Task WritePacketAsync(byte[] bytes, int count, bool addCount, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
