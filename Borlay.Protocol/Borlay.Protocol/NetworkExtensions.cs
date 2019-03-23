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

        public static Task WritePacketAsync(this Stream stream, byte[] bytes, int count, bool addCount, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(() =>
            {
                WritePacket(stream, bytes, count, addCount);
            }, cancellationToken);
        }

        public static void WritePacket(this Stream stream, byte[] bytes, int count, bool addCount)
        {
            if (addCount)
            {
                var countBytes = ByteArrayExtensions.GetBytes((int)count);

                var arr = new byte[count + 4];
                Array.Copy(countBytes, arr, 4);
                Array.Copy(bytes, 0, arr, 4, count);

                stream.Write(arr, 0, count + 4);
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
                ReadPacket(stream, buffer, 4, cancellationToken);
                var length = buffer.GetValue<int>(4, 0);
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
            ReadPacket(stream, buffer, 4, cancellationToken);
            var length = buffer.GetValue<int>(4, 0);
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
