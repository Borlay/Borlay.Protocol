//using Borlay.Queue;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.IO;
//using System.Net.Sockets;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Borlay.Protocol
//{
//    public class SocketStream : Stream
//    {
//        private readonly Socket socket;

//        public override bool CanRead => true;

//        public override bool CanSeek => false;

//        public override bool CanWrite => true;

//        public override long Length => socket.Available;
//        public override long Position { get => 0; set => throw new NotImplementedException(); }

//        private readonly AsyncQueue<byte> bytes = new AsyncQueue<byte>();

//        private readonly byte[] readBuffer = new byte[2048];

//        public SocketStream(Socket socket)
//        {
//            this.socket = socket;
//            //Listen(CancellationToken.None);
//        }

//        public async void Listen(CancellationToken cancellationToken)
//        {
//            await Task.Factory.StartNew(() =>
//            {
//                do
//                {
//                    var count = socket.Receive(readBuffer, 0, readBuffer.Length, SocketFlags.None);
//                    for (int i = 0; i < count; i++)
//                    {
//                        bytes.Enqueue(readBuffer[i]);
//                    }
//                } while (!cancellationToken.IsCancellationRequested);
//            });
//        }

//        //public Task<int> ReadBufferAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
//        //{
//        //    TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
//        //    cancellationToken.Register(() => tcs.TrySetCanceled());
//        //    var receiveArg = new SocketAsyncEventArgs();
//        //    receiveArg.SetBuffer(buffer, offset, count);
//        //    receiveArg.Completed += (s, e) =>
//        //    {
//        //        if (e.SocketError != SocketError.Success)
//        //            tcs.TrySetException(new SocketException((int)e.SocketError));

//        //        tcs.TrySetResult(e.BytesTransferred);
//        //    };
//        //    socket.ReceiveAsync(receiveArg);
//        //    return tcs.Task;
//        //}

//        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
//        {
//            return await Task.Factory.StartNew<int>(() =>
//            {
//                var totalReceived = 0;
//                do
//                {
//                    var received = socket
//                        .Receive(buffer, totalReceived, (count - totalReceived), SocketFlags.None, out var socketError);
//                    totalReceived += received;

//                    if (received == 0) return 0;
//                    if (socketError != SocketError.Success)
//                        throw new SocketException((int)socketError);

//                    //if (received == 0)
//                    //    throw new ConnectionException(ConnectionError.Disconnected);

//                    cancellationToken.ThrowIfCancellationRequested();
//                } while (totalReceived != count);

//                return totalReceived;
//            });

//            //for (var i = 0; i < count; i++)
//            //{
//            //    var b = await bytes.DequeueAsync(cancellationToken);
//            //    buffer[offset + i] = b;
//            //}

//            //TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
//            //cancellationToken.Register(() => tcs.TrySetCanceled());
//            //var receiveArg = new SocketAsyncEventArgs();
//            //receiveArg.SetBuffer(buffer, offset, count);
//            //receiveArg.Completed += (s, e) =>
//            //{
//            //    if (e.SocketError != SocketError.Success)
//            //        tcs.TrySetException(new SocketException((int)e.SocketError));

//            //    tcs.TrySetResult(e.BytesTransferred);
//            //};
//            //socket.ReceiveAsync(receiveArg);
//            //return tcs.Task;
//        }

//        //public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
//        //{
//        //    TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
//        //    cancellationToken.Register(() => tcs.TrySetCanceled());
//        //    var receiveArg = new SocketAsyncEventArgs();
//        //    receiveArg.SetBuffer(buffer, offset, count);
//        //    receiveArg.Completed += (s, e) =>
//        //    {
//        //        if (e.SocketError != SocketError.Success)
//        //            tcs.TrySetException(new SocketException((int)e.SocketError));

//        //        tcs.TrySetResult(e.BytesTransferred);
//        //    };
//        //    socket.ReceiveAsync(receiveArg);
//        //    return tcs.Task;
//        //}

//        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
//        {
//            await Task.Factory.StartNew(() =>
//            {
//                var send = socket.Send(buffer, offset, count, SocketFlags.None, out var socketError);
//                if (socketError != SocketError.Success)
//                    throw new SocketException((int)socketError);
//            });
//        }

//        public override void Flush()
//        {
//            throw new NotImplementedException();
//        }

//        public override int Read(byte[] buffer, int offset, int count)
//        {
//            throw new NotImplementedException();
//        }

//        public override long Seek(long offset, SeekOrigin origin)
//        {
//            throw new NotImplementedException();
//        }

//        public override void SetLength(long value)
//        {
//            throw new NotImplementedException();
//        }

//        public override void Write(byte[] buffer, int offset, int count)
//        {
//            throw new NotImplementedException();
//        }
//    }
//}
