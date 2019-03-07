using Borlay.Arrays;
using Borlay.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Borlay.Protocol
{
    public static class NetworkExtensions
    {
        public static async Task<T> ThrowEmpty<T>(this Task<T> task) where T: class
        {
            var result = await task;
            if (result == null)
                throw new ArgumentNullException(nameof(result)); // todo change exception

            return result;
        }

        public static async Task<T> DefaultOnThrow<T>(this Task<T> task)
        {
            try
            {
                var result = await task;
                return result;
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return default(T);
            }
        }

        public static async Task WritePacketAsync(this Stream stream, byte[] bytes, CancellationToken cancellationToken)
        {
            await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
        }

        public static Task WritePacketAsync(this Stream stream, byte[] bytes, ushort count, bool addCount, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(() =>
            {
                WritePacket(stream, bytes, count, addCount);
            }, cancellationToken);
        }

        public static void WritePacket(this Stream stream, byte[] bytes, ushort count, bool addCount)
        {
            if (addCount)
            {
                var countBytes = ByteArrayExtensions.GetBytes((ushort)count);

                var arr = new byte[count + 2];
                Array.Copy(countBytes, arr, 2);
                Array.Copy(bytes, 0, arr, 2, count);

                stream.Write(arr, 0, count + 2);
            }
            else
            {
                stream.Write(bytes, 0, count);
            }
        }

        public static Task<int> ReadPacketAsync(this Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew<int>(() =>
            {
                ReadPacket(stream, buffer, 2, cancellationToken);
                var length = buffer.GetValue<ushort>(2, 0);
                ReadPacket(stream, buffer, length, cancellationToken);

                return length;
            });
        }

        public static Task<int> ReadPacketAsync(this Stream stream, byte[] buffer, int length, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew<int>(() =>
            {
                ReadPacket(stream, buffer, length, cancellationToken);
                return length;
            });
        }

        public static int ReadPacket(this Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            ReadPacket(stream, buffer, 2, cancellationToken);
            var length = buffer.GetValue<ushort>(2, 0);
            ReadPacket(stream, buffer, length, cancellationToken);
            return length;
        }

        public static void ReadPacket(this Stream stream, byte[] buffer, int length, CancellationToken cancellationToken)
        {
            if (length == 0)
                throw new ArgumentException($"{nameof(length)} shoud be greater than zero");

            if (length > buffer.Length)
                throw new ArgumentException($"{nameof(length)} shoud be less than buffer size ({buffer.Length})");

            var totalReceived = 0;

            using (var registration = cancellationToken.Register(() => stream.Dispose()))
            {
                do
                {
                    var received = stream
                        .Read(buffer, totalReceived, (length - totalReceived));
                    totalReceived += received;

                    if (received == 0)
                        throw new ConnectionException(ConnectionError.Disconnected);

                    cancellationToken.ThrowIfCancellationRequested();
                } while (totalReceived != length);
            }
        }
    }
}
