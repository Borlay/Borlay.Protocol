using Borlay.Serialization.Converters;
using System;
using System.Collections.Generic;

namespace Borlay.Protocol.Converters
{
    public class ProtocolConverter : IProtocolConverter
    {
        private readonly ISerializer serializer;

        public ProtocolConverter(ISerializer serializer)
        {
            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            this.serializer = serializer;
            this.serializer.AddConverter<RequestHeader>(new RequestHeaderConverter(), 30501);
            this.serializer.AddConverter<ConverterHeader>(new ConverterHeaderConverter(), 30521);
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
            destination[index++] = serializer.SerializerType;
            serializer.AddBytes(dataContext.Data, destination, ref index);
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

            if (serializerType != serializer.SerializerType)
                throw new NotSupportedException($"Serializer of type '{serializerType}' is not supported");

            var data = serializer.GetObject(source, ref index);

            var dataContext = new DataContext()
            {
                Data = data,
                DataFlag = dataFlag,
            };

            return dataContext;
        }
    }
}
