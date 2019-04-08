using Borlay.Serialization.Converters;
using System;
using System.Collections.Generic;

namespace Borlay.Protocol.Converters
{
    public class ProtocolConverter : IProtocolConverter
    {
        public ISerializer Serializer { get; }

        public ProtocolConverter(ISerializer serializer)
        {
            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            this.Serializer = serializer;
            this.Serializer.ConverterProvider.AddConverter<RequestHeader>(new RequestHeaderConverter(), 30501);
            this.Serializer.ConverterProvider.AddConverter<ConverterHeader>(new ConverterHeaderConverter(), 30521);
        }

        public virtual void Apply(byte[] destination, ref int index, params DataContext[] dataContexts)
        {
            foreach (var dataContext in dataContexts)
            {
                ConvertSingle(destination, ref index, dataContext);
            }
        }

        protected virtual void ConvertSingle(byte[] destination, ref int index, DataContext dataContext)
        {
            destination[index++] = dataContext.DataFlag.InternalValue;
            destination[index++] = Serializer.Type;
            if (dataContext.Bytes != null && dataContext.Bytes.Length > 0)
            {
                Array.Copy(dataContext.Bytes, 0, destination, index, dataContext.Bytes.Length);
                index += dataContext.Bytes.Length;
            }
            else
            {
                var beginIndex = index;
                Serializer.AddBytes(dataContext.Data, destination, ref index);
                var endIndex = index;
                if(!dataContext.Length.HasValue)
                    dataContext.Length = endIndex - beginIndex;
            }
        }

        public DataContext[] Resolve(byte[] source, ref int index, int length) //, out int dataAtIndex)
        {
            List<DataContext> dataContexts = new List<DataContext>();
            while (index < length)
            {
                var dataContext = Resolve(source, ref index);
                dataContexts.Add(dataContext);
            }

            return dataContexts.ToArray();
        }

        public DataContext Resolve(byte[] source, ref int index)
        {
            var dataFlag = (DataFlag)source[index++];
            var serializerType = source[index++];

            if (serializerType != Serializer.Type)
                throw new NotSupportedException($"Serializer of type '{serializerType}' is not supported");

            var beginIndex = index;
            var data = Serializer.GetObject(source, ref index);
            var endIndex = index;

            var dataContext = new DataContext()
            {
                Data = data,
                DataFlag = dataFlag,
                Length = endIndex - beginIndex
            };

            return dataContext;
        }

        public bool TryResolve(byte[] source, ref int index, DataFlag flag, out DataContext dataContext)
        {
            dataContext = null;
            var dataFlag = (DataFlag)source[index];
            if(dataFlag != flag)
                return false;

            index++;
            var serializerType = source[index++];

            if (serializerType != Serializer.Type)
                throw new NotSupportedException($"Serializer of type '{serializerType}' is not supported");

            var data = Serializer.GetObject(source, ref index);

            dataContext = new DataContext()
            {
                Data = data,
                DataFlag = dataFlag,
            };

            return true;
        }
    }
}
