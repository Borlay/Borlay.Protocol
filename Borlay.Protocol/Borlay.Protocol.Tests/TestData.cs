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
    public interface IAddMethod
    {
        [Action]
        Task<CalculatorResult> AddAsync(int first, int second);
    }

    public class AddMethod : IAddMethod
    {
        private readonly CalculatorParameter calculatorParameter;

        public AddMethod(CalculatorParameter calculatorParameter)
        {
            // an example that shows that you can pass parameters from dependency injection to consructor.
            this.calculatorParameter = calculatorParameter ?? throw new ArgumentNullException(nameof(calculatorParameter));
        }

        public async Task<CalculatorResult> AddAsync(int first, int second)
        {
            return new CalculatorResult() { Result = first + second };
        }
    }
}
