using Borlay.Arrays;
using Borlay.Handling;
using Borlay.Handling.Notations;
using Borlay.Injection;
using Borlay.Protocol.Converters;
using Borlay.Protocol.Injection;
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
    public class SocketProtocolHandler : IProtocolHandler //, IRequestAsync
    {
        public const int DefaultBufferSize = 4096;
        public int BufferSize { get; protected set; } = DefaultBufferSize;

        public event Action<SocketProtocolHandler> Closed = (p) => { };

        //protected readonly ICache dataCache;
        //protected readonly IRequestKeyCache responseKeyCache = new RequestKeyCache();
        //protected readonly IRequestKeyCache requestKeyCache = new RequestKeyCache();

        protected readonly IPacketStream packetStream;
        //protected readonly ISerializer serializer;
        //protected readonly IHandlerProvider handlerProvider;
        protected readonly IProtocolHandler protocolHandler;
        protected readonly IProtocolConverter protocolConverter;

        
        public ConverterHeader ConverterHeader { get; set; }

        protected byte[] sendBuffer = new byte[DefaultBufferSize];
        protected byte[] readBuffer = new byte[DefaultBufferSize];

        protected ConcurrentDictionary<int, TaskCompletionSource<DataContent>> responses = 
            new ConcurrentDictionary<int, TaskCompletionSource<DataContent>>();

        protected int lastRequestId = 0;
        protected volatile bool closed = false;
        protected volatile bool listening = false;

        public IDataInject DataInject { get; set; }

        public ISecurityInject SecurityInject { get; set; }

        public IResolverSession ResolverSession { get; }


        public SocketProtocolHandler(IResolverSession session, Stream stream, ISerializer serializer, IHandlerProvider handlerProvider)
            : this(session, new PacketStream(stream), serializer, handlerProvider, new Cache())
        {
        }

        public SocketProtocolHandler(IResolverSession session, IPacketStream packetStream, ISerializer serializer, IHandlerProvider handlerProvider)
            : this(session, packetStream, serializer, handlerProvider, new Cache())
        {

        }

        public SocketProtocolHandler(IResolverSession session, IPacketStream packetStream, ISerializer serializer, IHandlerProvider handlerProvider, ICache cache)
            : this(session, packetStream, new ProtocolConverter(serializer), handlerProvider, cache)
        {
        }

        public SocketProtocolHandler(IResolverSession session, IPacketStream packetStream, IProtocolConverter protocolConverter, IHandlerProvider handlerProvider)
            : this(session, packetStream, protocolConverter, handlerProvider, new Cache())
        {
        }

        public SocketProtocolHandler(IResolverSession session, Stream stream, IProtocolConverter protocolConverter, IHandlerProvider handlerProvider)
            : this(session, new PacketStream(stream), protocolConverter, handlerProvider, new Cache())
        {
        }

        public SocketProtocolHandler(IResolverSession session, IPacketStream packetStream, IProtocolConverter protocolConverter, IHandlerProvider handlerProvider, ICache cache)
        {
            this.ResolverSession = session;
            this.packetStream = packetStream ?? throw new ArgumentNullException(nameof(packetStream));
            //this.handlerProvider = handlerProvider ?? throw new ArgumentNullException(nameof(handlerProvider));
            this.protocolHandler = new MethodProtocolHandler(handlerProvider);
            //this.dataCache = cache ?? throw new ArgumentNullException(nameof(cache));

            this.protocolConverter = protocolConverter;

            this.ConverterHeader = new ConverterHeader() { VersionMajor = 1 };
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

        public virtual Task<DataContent> HandleDataAsync(IResolverSession session, DataContent dataContent, CancellationToken cancellationToken)
        {
            var stop = ProtocolWatch.Start("send-request");
            var taskSource = new TaskCompletionSource<DataContent>();

            Monitor.Enter(this);
            try
            {
                ThrowClosed();

                var requestId = lastRequestId++;

                var requestHeader = new RequestHeader()
                {
                    RequestId = requestId,
                    CanBeCached = false, //actionMeta.CanBeCached,
                    RequestType = RequestType.Request,
                    RezervedFlag = 0
                };


                var index = PrepareSendData(sendBuffer, requestHeader, dataContent);

                responses[requestId] = taskSource;

                cancellationToken.Register(() =>
                {
                    if (responses.TryRemove(requestId, out var tcs))
                        tcs.TrySetCanceled();
                });

                //if(actionMeta.CacheReceivedResponse)
                //{
                //    var key = responseKeyCache.GetKey(sendBuffer, keyStartIndex, index - keyStartIndex);
                //    if(!dataCache.Contains(key))
                //        responseKeyCache.SaveRequestKey(requestId, key);
                //}

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

        protected virtual int PrepareSendData(byte[] sendBuffer, RequestHeader requestHeader, DataContent dataContent)
        {
            var index = 4;

            var converterHeader = this.ConverterHeader;
            var converterHeaderContext = new DataContext()
            {
                Data = converterHeader,
                DataFlag = DataFlag.Header,
            };

            var requestHeaderContext = new DataContext()
            {
                Data = requestHeader,
                DataFlag = DataFlag.Header
            };

            DataContext[] dataContexts = null;
            if (DataInject != null)
            {
                var injectContext = new DataInjectContext()
                {
                    ConverterHeader = ConverterHeader,
                    RequestHeader = requestHeader,
                    DataContent = dataContent,
                };

                dataContexts = DataInject.SendData(ResolverSession, injectContext)?.ToArray();
                converterHeader = injectContext.ConverterHeader;
                converterHeaderContext.Data = converterHeader;
            }

            protocolConverter.Apply(sendBuffer, ref index, converterHeaderContext, requestHeaderContext);
            var headerEndIndex = index;

            if (dataContexts != null)
            {
                protocolConverter.Apply(sendBuffer, ref index, dataContexts);
            }

            var dataStartIndex = index;
            protocolConverter.Apply(sendBuffer, ref index, dataContent.DataContexts);

            if (SecurityInject != null)
            {
                var securityContext = new SecurityInjectContext()
                {
                    ConverterHeader = converterHeader,
                    RequestHeader = requestHeader,
                    HeaderEndIndex = headerEndIndex,
                    Data = sendBuffer,
                    Length = index
                };

                SecurityInject?.SendSecurity(ResolverSession, securityContext);
                index = securityContext.Length;
            }

            sendBuffer.InsertLength(index - 4);

            return index;
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

                        HandlePacket(readBuffer, length, cancellationToken);

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

        protected virtual void HandlePacket(byte[] readBuffer, int length, CancellationToken cancellationToken)
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


            if (SecurityInject != null)
            {
                var securityContext = new SecurityInjectContext()
                {
                    ConverterHeader = converterHeader,
                    RequestHeader = requestHeader,
                    HeaderEndIndex = index,
                    Data = sendBuffer,
                    Length = length
                };

                SecurityInject?.ReceiveSecurity(ResolverSession, securityContext);
                length = securityContext.Length;
            }

            var resolvedContexts = protocolConverter.Resolve(readBuffer, ref index, length);
            var resolvedContent = new DataContent(resolvedContexts);

            if (DataInject != null)
            {
                var injectContext = new DataInjectContext()
                {
                    ConverterHeader = converterHeader,
                    RequestHeader = requestHeader,
                    DataContent = resolvedContent,
                };

                DataInject.ReceiveData(ResolverSession, injectContext);
            }

            stop();


            var requestId = requestHeader.RequestId;

            if (requestHeader.RequestType == RequestType.Response)
            {
                HandleResponse(resolvedContent, requestId, requestHeader.CanBeCached, index);
            }
            else if (requestHeader.RequestType == RequestType.Request)
            {
                HandleRequest(resolvedContent, requestId, index, cancellationToken);
            }
            else
                throw new ProtocolException(ErrorCode.BadRequest);
        }

        protected virtual void HandleResponse(DataContent dataContent, int requestId, bool canBeCached, int index)
        {
            var stop = ProtocolWatch.Start("rp-handle-response");

            if (responses.TryRemove(requestId, out var tcs))
            {
                try
                {
                    if (tcs.Task.IsCompleted || tcs.Task.IsCanceled || tcs.Task.IsFaulted)
                        return;

                    //var dataStartIndex = index;

                    //var response = contexts[DataFlag.Data].First().Data;

                    //var isEmpty = IsEmptyOrError(response, false);
                    //if (isEmpty)
                    //    tcs.TrySetResult(null);


                    //if (responseKeyCache.TryRemoveRequestKey(requestId, out var key) && canBeCached && !isEmpty)
                    //    dataCache.AddData(key, readBuffer, dataStartIndex, index - dataStartIndex);

                    tcs.TrySetResult(dataContent);
                    stop();
                }
                catch (Exception e)
                {
                    tcs.TrySetException(e);
                }
            }
        }

        protected virtual async void HandleRequest(DataContent dataContent, int requestId, int index, CancellationToken cancellationToken)
        {
            var cache = false;
            try
            {
                var stop = ProtocolWatch.Start("rp-handle-request");
                var result = await protocolHandler.HandleDataAsync(ResolverSession, dataContent, cancellationToken);

                //object response = await handler.HandleAsync(ResolverSession, request, cancellationToken);
                //if (response == null)
                //{
                //    response = new EmptyResponse();
                //    canBeCached = false;
                //}

                stop();
                SendResponse(result, requestId, false, cache);

                //var keyStartIndex = index;

                //var actionId = contexts[DataFlag.Action].First()?.Data; //protocolConverter.Resolve<object>(readBuffer, ref index, DataFlag.Action);
                //var scopeId = contexts[DataFlag.Scope].FirstOrDefault()?.Data;
                //var methodHash = contexts[DataFlag.MethodHash].FirstOrDefault()?.Data;
                //var request = contexts[DataFlag.Data].Select(d => d.Data).ToArray(); //protocolConverter.Resolve<object>(readBuffer, ref index, DataFlag.Data);

                //if (methodHash == null || !(methodHash is ByteArray mhash))
                //    throw new ProtocolException("Parameter hash is null or not ByteArray", ErrorCode.BadRequest);

                ////var isEmpty = IsEmptyOrError(request, false);
                ////if (isEmpty)
                ////    throw new ProtocolException(ErrorCode.UnknownRequest);

                //// todo add actionId
                //if(!handlerProvider.TryGetHandler(scopeId ?? "", actionId ?? "", mhash, out var handlerItem))
                //    throw new ProtocolException($"Handler for scope {scopeId} action {actionId} hash {methodHash} not found", ErrorCode.BadRequest);


                //var actionMeta = handlerItem.ActionMeta ?? throw new ArgumentNullException(nameof(handlerItem.ActionMeta));

                //if (actionMeta.CanBeCached)
                //{
                //    var key = responseKeyCache.GetKey(readBuffer, keyStartIndex, index - keyStartIndex);
                //    if (dataCache.TryGetData(key, out var bytes))
                //    {
                //        SendResponse(requestId, bytes, actionMeta.CanBeCached);
                //        return;
                //    }
                //    else if (actionMeta.CacheSendedResponse)
                //    {
                //        cache = true;
                //        requestKeyCache.SaveRequestKey(requestId, key);
                //    }
                //}

                //stop();

                //HandleRequestAsync(requestId, handlerItem, request, false /* actionMeta.CanBeCached*/ , cache, cancellationToken);
            }
            catch(Exception e)
            {
                var response = CreateErrorResponse(e);
                SendResponse(requestId, response, false, cache);
            }
        }

        protected virtual async void HandleRequestAsync(int requestId, IHandler handler, object[] request, bool canBeCached, bool cache, CancellationToken cancellationToken)
        {
            try
            {
                
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
            var dataContent = new DataContent(new DataContext()
            {
                DataFlag = DataFlag.Data,
                Data = response,
            });
            SendResponse(dataContent, requestId, canBeCached, cache);
        }


        public virtual void SendResponse(DataContent dataContent, int requestId, bool canBeCached, bool cache)
        {
            var stop = ProtocolWatch.Start("rp-send-response");
            Monitor.Enter(this);
            try
            {
                ThrowClosed();

                var requestHeader = new RequestHeader()
                {
                    RequestId = requestId,
                    RequestType = RequestType.Response,
                    CanBeCached = canBeCached,
                    RezervedFlag = 0,
                };

                //var dataDataContext = new DataContext()
                //{
                //    DataFlag = DataFlag.Data,
                //    Data = response,
                //};


                var index = PrepareSendData(sendBuffer, requestHeader, dataContent);

                //protocolConverter.Apply(sendBuffer, ref index, converterHeaderContext);
                //protocolConverter.ApplyHeader(sendBuffer, ref index, header);

                //var dataStartIndex = index;

                //protocolConverter.ApplyData(sendBuffer, ref index, response);

                //sendBuffer.InsertLength(index - 2);

                //if (cache && requestKeyCache.TryRemoveRequestKey(requestId, out var key) && canBeCached)
                //    dataCache.AddData(key, sendBuffer, dataStartIndex, index - dataStartIndex);

                packetStream.WritePacket(sendBuffer, (ushort)index, false);
            }
            catch (Exception e)
            {
                //requestKeyCache.RemoveRequestKey(requestId);
                OnClosed(e);
                throw;
            }
            finally
            {
                Monitor.Exit(this);
                stop();
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
