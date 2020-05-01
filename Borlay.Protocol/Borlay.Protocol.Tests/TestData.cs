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
    public class CalculatorParameter
    {
        public int First { get; set; }
    }

    [Resolve(Singletone = false)]
    [Handler]
    public interface IAddMethod //: IMerge
    {
        [Action]
        Task<CalculatorResult> AddAsync(int first, int second);
    }

    public class AddMethod : IAddMethod
    {
        private readonly CalculatorParameter calculatorParameter;
        private readonly IResolverSession resolverSession;

        public AddMethod(CalculatorParameter calculatorParameter)
        {
            this.calculatorParameter = calculatorParameter;
        }

        public async Task<CalculatorResult> AddAsync(int first, int second)
        {
            return new CalculatorResult() { Result = first + second };
        }
    }
}
