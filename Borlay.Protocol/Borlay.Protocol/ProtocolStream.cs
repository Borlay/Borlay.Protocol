using Borlay.Arrays;
using Borlay.Handling;
using Borlay.Handling.Notations;
using Borlay.Protocol.Converters;
using Borlay.Serialization;
using Borlay.Serialization.Converters;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Borlay.Protocol
{
    public class ProtocolStream : IRequestAsync
    {
        public const int DefaultBufferSize = 2048;
        public int BufferSize { get; protected set; } = DefaultBufferSize;

        public event Action<ProtocolStream> Closed = (p) => { };

        protected readonly ICache dataCache;
        protected readonly IRequestKeyCache responseKeyCache = new RequestKeyCache();
        protected readonly IRequestKeyCache requestKeyCache = new RequestKeyCache();

        protected readonly IPacketStream packetStream;
        //protected readonly ISerializer serializer;
        protected readonly IHandlerProvider handlerProvider;
        protected readonly IProtocolConverter protocolConverter;

        protected readonly DataContext converterHeaderContext;

        protected byte[] sendBuffer = new byte[DefaultBufferSize];
        protected byte[] readBuffer = new byte[DefaultBufferSize];

        protected ConcurrentDictionary<int, TaskCompletionSource<object>> responses = 
            new ConcurrentDictionary<int, TaskCompletionSource<object>>();

        protected int lastRequestId = 0;
        protected volatile bool closed = false;
        protected volatile bool listening = false;


        public ProtocolStream(Stream stream, ISerializer serializer, IHandlerProvider handlerProvider)
            : this(new PacketStream(stream), serializer, handlerProvider, new Cache())
        {
        }

        public ProtocolStream(IPacketStream packetStream, ISerializer serializer, IHandlerProvider handlerProvider)
            : this(packetStream, serializer, handlerProvider, new Cache())
        {

        }

        public ProtocolStream(IPacketStream packetStream, ISerializer serializer, IHandlerProvider handlerProvider, ICache cache)
        {
            this.packetStream = packetStream ?? throw new ArgumentNullException(nameof(packetStream));
            //this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            this.handlerProvider = handlerProvider ?? throw new ArgumentNullException(nameof(handlerProvider));
            this.dataCache = cache ?? throw new ArgumentNullException(nameof(cache));

            protocolConverter = new ProtocolConverter(serializer);

            converterHeaderContext = new DataContext()
            {
                Data = new ConverterHeader() { VersionMajor = 1 },
                DataFlag = DataFlag.Header,
            };
        }

        public async Task<T> SendRequestAsync<T>(IActionMeta actionMeta, object obj, bool throwOnEmpty, CancellationToken cancellationToken)
        {
            var responseObj = await SendRequestAsync(actionMeta, obj, throwOnEmpty, cancellationToken);
            var response = ValidateResponse<T>(responseObj, throwOnEmpty);
            return response;
        }

        /// <summary>
        /// Set send and read buffers size. This method is not thread safe.
        /// </summary>
        /// <param name="length">Buffer size</param>
        public virtual void SetBufferSize(int length)
        {
            sendBuffer = new byte[length];
            readBuffer = new byte[length];
            BufferSize = length;
        }

        // todo request cache

        public virtual Task<object> SendRequestAsync(IActionMeta actionMeta, object obj, bool throwOnEmpty, CancellationToken cancellationToken)
        {
            var stop = ProtocolWatch.Start("send-request");
            var taskSource = new TaskCompletionSource<object>();

            Monitor.Enter(this);
            try
            {
                ThrowClosed();

                var requestId = lastRequestId++;
                int index = 2;

                var requestHeader = new RequestHeader()
                {
                    RequestId = requestId,
                    CanBeCached = actionMeta.CanBeCached,
                    RequestType = RequestType.Request,
                    RezervedFlag = 0
                };

                var requestHeaderContext = new DataContext()
                {
                    Data = requestHeader,
                    DataFlag = DataFlag.Header
                };

                protocolConverter.Apply(sendBuffer, ref index, converterHeaderContext, requestHeaderContext);

                var keyStartIndex = index;

                protocolConverter.Apply(sendBuffer, ref index, actionMeta.GetActionId(), DataFlag.Action);
                protocolConverter.ApplyData(sendBuffer, ref index, obj);

                sendBuffer.InsertLength(index - 2);

                responses[requestId] = taskSource;

                cancellationToken.Register(() =>
                {
                    if (responses.TryRemove(requestId, out var tcs))
                        tcs.TrySetCanceled();
                });

                if(actionMeta.CacheReceivedResponse)
                {
                    var key = responseKeyCache.GetKey(sendBuffer, keyStartIndex, index - keyStartIndex);
                    if(!dataCache.Contains(key))
                        responseKeyCache.SaveRequestKey(requestId, key);
                }

                packetStream.WritePacket(sendBuffer, index, false);
            }
            catch (Exception e)
            {
                OnClosed(e);
                throw;
            }
            finally
            {
                Monitor.Exit(this);
                stop();
            }

            return taskSource.Task;
        }

        public virtual Task ListenAsync(CancellationToken cancellationToken)
        {
            ThrowClosed();
            CheckListening();

            return Task.Factory.StartNew(() =>
            {
                do
                {
                    try
                    {
                        var length = packetStream.ReadPacket(readBuffer, cancellationToken); // todo handlinti exceptionus
                        if (length < 11 || length > readBuffer.Length)
                            throw new ProtocolException(ErrorCode.BadRequest);

                        //var newArray = new byte[length];
                        //Array.Copy(readBuffer, newArray, length);
                        
                        HandlePacket(readBuffer, cancellationToken);

                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    catch (Exception e)
                    {
                        SetClosed(e);
                        throw;
                    }
                } while (!cancellationToken.IsCancellationRequested);
            }, TaskCreationOptions.LongRunning);
        }

        protected virtual void HandlePacket(byte[] readBuffer, CancellationToken cancellationToken)
        {
            ThrowClosed();

            var stop = ProtocolWatch.Start("rp-receive-request");

            var index = 0;

            var converterHeader = protocolConverter.Resolve<ConverterHeader>(readBuffer, ref index);
            var requestHeader = protocolConverter.Resolve<RequestHeader>(readBuffer, ref index);

            if (converterHeader.VersionMajor != 1)
                throw new ProtocolException(ErrorCode.VersionNotSupported);

            if (converterHeader.Encryption != 0)
                throw new ProtocolException(ErrorCode.VersionNotSupported);
            if (converterHeader.Compression != 0)
                throw new ProtocolException(ErrorCode.VersionNotSupported);

            stop();

            var requestId = requestHeader.RequestId;

            if (requestHeader.RequestType == RequestType.Response)
            {
                HandleResponse(readBuffer, requestId, requestHeader.CanBeCached, index);
            }
            else if (requestHeader.RequestType == RequestType.Request)
            {
                HandleRequest(readBuffer, requestId, index, cancellationToken);
            }
            else
                throw new ProtocolException(ErrorCode.BadRequest);
        }

        protected virtual void HandleResponse(byte[] readBuffer, int requestId, bool canBeCached, int index)
        {
            var stop = ProtocolWatch.Start("rp-handle-response");

            if (responses.TryRemove(requestId, out var tcs))
            {
                try
                {
                    if (tcs.Task.IsCompleted || tcs.Task.IsCanceled || tcs.Task.IsFaulted)
                        return;

                    var dataStartIndex = index;

                    var response = protocolConverter.Resolve<object>(readBuffer, ref index, DataFlag.Data);

                    var isEmpty = IsEmptyOrError(response, false);
                    if (isEmpty)
                        tcs.TrySetResult(null);

                    if (responseKeyCache.TryRemoveRequestKey(requestId, out var key) && canBeCached && !isEmpty)
                        dataCache.AddData(key, readBuffer, dataStartIndex, index - dataStartIndex);

                    tcs.TrySetResult(response);
                    stop();
                }
                catch (Exception e)
                {
                    tcs.TrySetException(e);
                }
            }
        }

        protected virtual void HandleRequest(byte[] readBuffer, int requestId, int index, CancellationToken cancellationToken)
        {
            var cache = false;
            try
            {
                var stop = ProtocolWatch.Start("rp-handle-request");

                var keyStartIndex = index;

                var actionId = protocolConverter.Resolve<object>(readBuffer, ref index, DataFlag.Action);
                var request = protocolConverter.Resolve<object>(readBuffer, ref index, DataFlag.Data);

                var isEmpty = IsEmptyOrError(request, false);
                if (isEmpty)
                    throw new ProtocolException(ErrorCode.UnknownRequest);

                // todo add actionId
                if(!handlerProvider.TryGetHandler("", actionId, new Type[] { request.GetType() }, out var handlerItem))
                    throw new ProtocolException(ErrorCode.BadRequest);

                var actionMeta = handlerItem.ActionMeta ?? throw new ArgumentNullException(nameof(handlerItem.ActionMeta));

                if (actionMeta.CanBeCached)
                {
                    var key = responseKeyCache.GetKey(readBuffer, keyStartIndex, index - keyStartIndex);
                    if (dataCache.TryGetData(key, out var bytes))
                    {
                        SendResponse(requestId, bytes, actionMeta.CanBeCached);
                        return;
                    }
                    else if (actionMeta.CacheSendedResponse)
                    {
                        cache = true;
                        requestKeyCache.SaveRequestKey(requestId, key);
                    }
                }

                stop();

                HandleRequestAsync(requestId, handlerItem, request, actionMeta.CanBeCached, cache, cancellationToken);
            }
            catch(Exception e)
            {
                var response = CreateErrorResponse(e);
                SendResponse(requestId, response, false, cache);
            }
        }

        protected virtual async void HandleRequestAsync(int requestId, IHandler handler, object request, bool canBeCached, bool cache, CancellationToken cancellationToken)
        {
            try
            {
                var stop = ProtocolWatch.Start("rp-request-handler");
                var response = await handler.HandleAsync(request, cancellationToken);
                if (response == null)
                {
                    response = new EmptyResponse();
                    canBeCached = false;
                }
                stop();
                SendResponse(requestId, response, canBeCached, cache);
            }
            catch(Exception e)
            {
                var response = CreateErrorResponse(e);
                SendResponse(requestId, response, false, cache);
            }
        }

        public virtual ErrorResponse CreateErrorResponse(Exception exception)
        {
            if(exception is VersionMismatchException)
            {
                return new ErrorResponse()
                {
                    Code = ErrorCode.VersionNotSupported,
                    Message = exception.Message
                };
            }
            if(exception is ProtocolException)
            {
                return new ErrorResponse()
                {
                    Code = ((ProtocolException)exception).ResponseError,
                    Message= exception.Message
                };
            }
            return new ErrorResponse()
            {
                Code = ErrorCode.BadRequest,
                Message = exception.Message
            };
        }

        public virtual void SendResponse(int requestId, object response, bool canBeCached, bool cache)
        {
            var stop = ProtocolWatch.Start("rp-send-response");
            Monitor.Enter(this);
            try
            {
                ThrowClosed();
                var index = 2;

                var header = new RequestHeader()
                {
                    RequestId = requestId,
                    RequestType = RequestType.Response,
                    CanBeCached = canBeCached,
                    RezervedFlag = 0,
                };

                protocolConverter.Apply(sendBuffer, ref index, converterHeaderContext);
                protocolConverter.ApplyHeader(sendBuffer, ref index, header);

                var dataStartIndex = index;

                protocolConverter.ApplyData(sendBuffer, ref index, response);

                sendBuffer.InsertLength(index - 2);

                if (cache && requestKeyCache.TryRemoveRequestKey(requestId, out var key) && canBeCached)
                    dataCache.AddData(key, sendBuffer, dataStartIndex, index - dataStartIndex);

                packetStream.WritePacket(sendBuffer, (ushort)index, false);
            }
            catch (Exception e)
            {
                requestKeyCache.RemoveRequestKey(requestId);
                OnClosed(e);
                throw;
            }
            finally
            {
                Monitor.Exit(this);
                stop();
            }
        }

        public virtual async void SendResponse(int requestId, byte[] response, bool canBeCached)
        {
            Monitor.Enter(this);
            try
            {
                ThrowClosed();
                var index = 2;

                var header = new RequestHeader()
                {
                    RequestId = requestId,
                    RequestType = RequestType.Response,
                    CanBeCached = canBeCached,
                    RezervedFlag = 0,
                };

                protocolConverter.Apply(sendBuffer, ref index, converterHeaderContext);
                protocolConverter.ApplyHeader(sendBuffer, ref index, header);

                sendBuffer.AddBytes(response, ref index);
                sendBuffer.InsertLength(index - 2);

                packetStream.WritePacket(sendBuffer, (ushort)index, false);
            }
            catch (Exception e)
            {
                OnClosed(e);
                throw;
            }
            finally
            {
                Monitor.Exit(this);
            }
        }


        public virtual T ValidateResponse<T>(object response, bool throwOnEmpty)
        {
            if (response == null)
                return default(T);

            if (response is T)
                return (T)response;

            throw new ProtocolException(ErrorCode.UnknownResponse);
        }

        public virtual bool IsEmptyOrError(object response, bool throwOnEmpty)
        {
            if (response is ErrorResponse)
            {
                var errorResponse = (ErrorResponse)response;

                if (errorResponse.Code == ErrorCode.DataNotFound && !throwOnEmpty)
                    return true;

                throw new ProtocolException(errorResponse.Message, errorResponse.Code);
            }

            if (response is EmptyResponse)
            {
                if (throwOnEmpty)
                    throw new EmptyResponseException();
                return true;
            }

            return false;
        }

        public virtual void CheckListening()
        {
            Monitor.Enter(this);
            try
            {
                if (listening)
                    throw new ConnectionException($"Protocol Stream already listening", ConnectionError.AlreadyListening);

                listening = true;
            }
            finally
            {
                Monitor.Exit(this);
            }

        }

        public virtual void SetClosed(Exception e)
        {
            Monitor.Enter(this);
            try
            {
                OnClosed(e);
            }
            finally
            {
                Monitor.Exit(this);
            }
        }

        protected virtual void OnClosed(Exception e)
        {
            if (closed) return;
            closed = true;

            try
            {
                foreach (var resp in responses)
                {
                    try
                    {
                        resp.Value.TrySetException(e);
                    }
                    catch { } // do nothing
                }
            }
            finally
            {
                Closed(this);
            }
        }

        protected virtual void ThrowClosed()
        {
            if (closed)
                throw new ProtocolException(ErrorCode.Closed);
        }
    }
}
