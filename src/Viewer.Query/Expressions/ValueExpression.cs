﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Viewer.Data;

namespace Viewer.Query.Expressions
{
    /// <summary>
    /// Expression whose result is a value (see <see cref="BaseValue"/>)
    /// </summary>
    internal abstract class ValueExpression
    {
        /// <summary>
        /// An expression which always evaluates to true when compiles to a predicate.
        /// </summary>
        public static ValueExpression True { get; } = 
            new ConstantExpression(0, 0, new IntValue(1));

        /// <summary>
        /// An expression which is always null (or false if compiled as predicate)
        /// </summary>
        public static ValueExpression Null { get; } = 
            new ConstantExpression(0, 0, new IntValue(null));

        /// <summary>
        /// Line in the query on which this value expression starts
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// Column in <see cref="Line"/> on which this expression starts
        /// </summary>
        public int Column { get; }

        /// <summary>
        /// Get all subexpressions of this expression
        /// </summary>
        public virtual IEnumerable<ValueExpression> Children => Enumerable.Empty<ValueExpression>();

        /// <summary>
        /// This should only be used in tests
        /// </summary>
        internal ValueExpression()
        {
        }

        protected ValueExpression(int line, int column)
        {
            if (line < 0)
                throw new ArgumentOutOfRangeException(nameof(line));
            if (column < 0)
                throw new ArgumentOutOfRangeException(nameof(column));

            Line = line;
            Column = column;
        }
        
        /// <summary>
        /// Compile this expression to a function which takes an entity and returns the computed
        /// value.
        /// </summary>
        /// <param name="runtime"></param>
        /// <returns></returns>
        public virtual Func<IEntity, BaseValue> CompileFunction(IRuntime runtime)
        {
            var entityParameter = Expression.Parameter(typeof(IEntity), "entity");
            var expression = ToExpressionTree(entityParameter, runtime);
            var functionExpression = Expression.Lambda<Func<IEntity, BaseValue>>(
                expression, 
                entityParameter);
            var function = functionExpression.Compile();
            return function;
        }

        /// <summary>
        /// Compile this expression to a predicate function which takes an entity and returns true
        /// iff the expression evaluates to a value which is not null.
        /// </summary>
        /// <param name="runtime"></param>
        /// <returns></returns>
        public virtual Func<IEntity, bool> CompilePredicate(IRuntime runtime)
        {
            var function = CompileFunction(runtime);
            return entity => !function(entity).IsNull;
        }

        public abstract override string ToString();

        public abstract T Accept<T>(IExpressionVisitor<T> visitor);

        public abstract Expression ToExpressionTree(
            ParameterExpression entityParameter, 
            IRuntime runtime);
    }
}
