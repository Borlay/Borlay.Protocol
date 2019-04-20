using Borlay.Protocol.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Borlay.Protocol
{
    public class DataContent : IEnumerable<DataContext>, ILookup<DataFlag, DataContext>
    {
        protected readonly ILookup<DataFlag, DataContext> contextLookup;

        public DataContext[] DataContexts { get; private set; }

        public int Count => contextLookup.Count;

        public DataContent(params DataContext[] contexts)
        {
            contextLookup = contexts.ToLookup(c => c.DataFlag);
            DataContexts = contexts;
        }

        public virtual IEnumerable<DataContext> this[DataFlag flag]
        {
            get
            {
                return contextLookup[flag];
            }
        }

        public IEnumerator<DataContext> GetEnumerator()
        {
            return DataContexts.AsEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return DataContexts.GetEnumerator();
        }

        public bool Contains(DataFlag key)
        {
            return contextLookup.Contains(key);
        }

        IEnumerator<IGrouping<DataFlag, DataContext>> IEnumerable<IGrouping<DataFlag, DataContext>>.GetEnumerator()
        {
            return contextLookup.GetEnumerator();
        }
    }

    public static class DataContentExtensions
    {
        public static object GetData(this DataContent content, byte type)
        {
            var data = content[type].SingleOrDefault()?.Data;
            return data;
        }

        public static T GetData<T>(this DataContent content, byte type) where T : class
        {
            var data = content[type].SingleOrDefault()?.Data;
            if (data == null) return null;
            if (data is T t) return t;

            throw new ArgumentException($"Data for data flag '{type}' is not of type '{typeof(T).FullName}' but '{data.GetType().FullName}'");
        }
    }

}
