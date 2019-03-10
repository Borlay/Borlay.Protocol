using Borlay.Handling.Notations;
using Borlay.Injection;
using Borlay.Serialization.Notations;
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
            var buffer = new byte[count];
            Array.Copy(bytes, 2, buffer, 0, count);
            packetStream.Append(buffer);
        }

        public Task WritePacketAsync(byte[] bytes, int count, bool addCount, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    [Data(100)]
    public class CalculatorArgument
    {
        [Include(0, true)]
        public int Left { get; set; }

        [Include(1, true)]
        public int Right { get; set; }
    }

    [Data(102)]
    public class CalculatorResult
    {
        [Include(0, true)]
        public int Result { get; set; }
    }

    public class CalculatorParameter
    {
        public int First { get; set; }
    }

    [Resolve]
    [Handler]
    public interface ICalculator //: IMerge
    {

        [IdAction(1, CanBeCached = true, CacheReceivedResponse = true)]
        Task<CalculatorResult> AddAsync(CalculatorArgument argument, [Inject]CancellationToken cancellationToken);

        [IdAction(1, CanBeCached = true, CacheReceivedResponse = true)]
        Task<CalculatorResult> AddAsync(string argument);

        [IdAction(1, CanBeCached = true, CacheReceivedResponse = true)]
        Task<CalculatorResult> AddAsync();

        [IdAction(1, CanBeCached = true, CacheReceivedResponse = true)]
        Task<CalculatorResult> AddAsync(CalculatorArgument argument, CalculatorArgument argument2, [Inject]CancellationToken cancellationToken);

        [NameAction]
        Task<CalculatorResult> Subsync(CalculatorArgument argument, [Inject]CancellationToken cancellationToken);
    }

    [Resolve]
    [Handler]
    [Role("Merge")]
    public interface IMerge
    {
        [NameAction]
        Task<CalculatorResult> MergeAsync(CalculatorArgument argument, [Inject]CancellationToken cancellationToken);
    }


    public class Calculator : ICalculator, IMerge
    {
        private readonly CalculatorParameter calculatorParameter;

        public Calculator(CalculatorParameter calculatorParameter)
        {
            this.calculatorParameter = calculatorParameter;
        }

        public async Task<CalculatorResult> AddAsync(CalculatorArgument argument, [Inject]CancellationToken cancellationToken)
        {
            return new CalculatorResult() { Result = argument.Left + argument.Right + calculatorParameter.First };
        }

        public async Task<CalculatorResult> AddAsync(string argument)
        {
            return new CalculatorResult() { Result = calculatorParameter.First + int.Parse(argument) };
        }

        public async Task<CalculatorResult> AddAsync()
        {
            return new CalculatorResult() { Result = calculatorParameter.First };
        }

        public async Task<CalculatorResult> AddAsync(CalculatorArgument argument, CalculatorArgument argument2, [Inject]CancellationToken cancellationToken)
        {
            return new CalculatorResult() { Result = argument.Left + argument.Right + argument2.Left + argument2.Right + calculatorParameter.First };
        }

        public async Task<CalculatorResult> MergeAsync(CalculatorArgument argument, [Inject]CancellationToken cancellationToken)
        {
            return new CalculatorResult() { Result = argument.Left * argument.Right * calculatorParameter.First };
        }

        public async Task<CalculatorResult> Subsync(CalculatorArgument argument, [Inject]CancellationToken cancellationToken)
        {
            return new CalculatorResult() { Result = argument.Left - argument.Right - calculatorParameter.First };
        }
    }
}
