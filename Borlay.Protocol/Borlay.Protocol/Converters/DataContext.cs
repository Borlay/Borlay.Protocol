
namespace Borlay.Protocol.Converters
{ 
    public class DataContext
    {
        public DataFlag DataFlag { get; set; }

        public object Data { get; set; }

        public int? Length { get; set; }

        public byte[] Bytes { get; set; }
    }
}
