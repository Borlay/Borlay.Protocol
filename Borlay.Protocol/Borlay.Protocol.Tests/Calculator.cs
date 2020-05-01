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
    [Resolve(Singletone = false)]
    [Handler]
    public interface ICalculator //: IMerge
    {
        [Action("1")]
        Task<CalculatorResult> AddAsync(CalculatorArgument argument, [Inject]CancellationToken cancellationToken);

        [Action("2")]
        Task<CalculatorResult> AddAsync(string argument);

        [Action("2")]
        Task<CalculatorResult> AddAsync();

        [Action("2")]
        Task<CalculatorResult> AddAsync(CalculatorArgument argument, CalculatorArgument argument2, [Inject]CancellationToken cancellationToken);

        [Action]
        Task<CalculatorResult> Subsync(CalculatorArgument argument, [Inject]CancellationToken cancellationToken);
    }

    public class Calculator : ICalculator, IMerge
    {
        private readonly CalculatorParameter calculatorParameter;
        private readonly IResolverSession resolverSession;

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


    [Resolve]
    [Handler]
    [Role("Merge")]
    public interface IMerge
    {
        [Action]
        Task<CalculatorResult> MergeAsync(CalculatorArgument argument, [Inject]CancellationToken cancellationToken);
    }

    [Resolve]
    [Handler]
    [Scope("GenericType")]
    public interface IGenericType<T>
    {
        [Action("0")]
        Task<int> Sum(T entity);
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

    public class GenericTypeHandler : IGenericType<CalculatorArgument>
    {
        public async Task<int> Sum(CalculatorArgument argument)
        {
            return argument.Left + argument.Right;
        }
    }

    public interface ICalculatorMerge : ICalculator, IMerge
    {

    }
}
