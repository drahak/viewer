﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Viewer.Data;
using Viewer.Data.Formats.Attributes;
using Viewer.Query.Expressions;
using Attribute = Viewer.Data.Attribute;
using ConstantExpression = Viewer.Query.Expressions.ConstantExpression;

namespace Viewer.Query
{
    public interface IQueryCompiler
    {
        /// <summary>
        /// Repository of views available to this compiler
        /// </summary>
        IQueryViewRepository Views { get; }

        /// <summary>
        /// Compile given query to an internal structure which can then be evaluated.
        /// </summary>
        /// <param name="input">Stream with the query</param>
        /// <param name="queryErrorListener">Error reporter</param>
        /// <returns>Compiled query</returns>
        IExecutableQuery Compile(TextReader input, IQueryErrorListener queryErrorListener);

        /// <summary>
        /// Same as Compile(new StringReader(query), defaultQueryErrorListener)
        /// </summary>
        /// <param name="query">Query to compile</param>
        /// <returns>Compiled query or null if there were errors during compilation</returns>
        IExecutableQuery Compile(string query);
    }
    
    internal class QueryCompilationListener : IQueryParserListener
    {
        private readonly Stack<IQuery> _queries = new Stack<IQuery>();
        private readonly Stack<ValueExpression> _expressions = new Stack<ValueExpression>();
        private readonly Stack<EntityComparer> _comparers = new Stack<EntityComparer>();
        private readonly Stack<int> _expressionsFrameStart = new Stack<int>();

        private readonly IQueryFactory _queryFactory;
        private readonly QueryCompiler _compiler;
        private readonly IQueryErrorListener _errorListener;
        private readonly IRuntime _runtime;
        private readonly QueryCompilationParameters _parameters;

        public QueryCompilationListener(
            IQueryFactory queryFactory,
            QueryCompiler compiler,
            IQueryErrorListener errorListener,
            IRuntime runtime,
            QueryCompilationParameters parameters)
        {
            _queryFactory = queryFactory;
            _compiler = compiler;
            _errorListener = errorListener;
            _runtime = runtime;
            _parameters = parameters;
        }

        public IQuery Finish()
        {
            var query = _queries.Pop();
            Trace.Assert(query != null, "query != null");
            Trace.Assert(_queries.Count == 0, "_queries.Count == 0");
            return query;
        }

        /// <summary>
        /// This method will compile a sequence of left associative binary
        /// <paramref name="operators"/> with the same priority. All operands are fetched from the
        /// <paramref name="stack"/>. Operator is evaluated using the
        /// <paramref name="applyOperator"/> function.
        /// </summary>
        /// <typeparam name="T">Type of operands</typeparam>
        /// <param name="operators">Operator terminals</param>
        /// <param name="stack">Stack with operands</param>
        /// <param name="applyOperator">Operator evaluation function</param>
        private void CompileLeftAssociativeOperator<T>(
            ITerminalNode[] operators,
            Stack<T> stack,
            Func<ITerminalNode, T, T, T> applyOperator)
        {
            if (operators.Length <= 0)
                return;
            
            // fetch operands
            var operands = new List<T>
            {
                stack.Pop()
            };

            for (var i = 0; i < operators.Length; ++i)
            {
                if (stack.Count <= 0)
                {
                    break;
                }
                operands.Add(stack.Pop());
            }

            Trace.Assert(
                operands.Count == operators.Length + 1, 
                "operands.Count == operators.Length + 1");

            // make sure we apply operators from left to right 
            operands.Reverse();

            // apply operators
            var result = operands[0];
            for (var i = 0; i < operators.Length; ++i)
            {
                result = applyOperator(operators[i], result, operands[i + 1]);
            }
            stack.Push(result);
        }

        public void VisitTerminal(ITerminalNode node)
        {
        }

        public void VisitErrorNode(IErrorNode node)
        {
        }

        public void EnterEveryRule(ParserRuleContext ctx)
        {
        }

        public void ExitEveryRule(ParserRuleContext ctx)
        {
        }

        public void EnterEntry(QueryParser.EntryContext context)
        {
        }

        public void ExitEntry(QueryParser.EntryContext context)
        {
        }

        #region Query expression (UNION, EXCEPT and INTERSECT)

        // All methods in this group are either no-ops or they pop 1 or more queries from the
        // _queries stack and push one query to the _queries stack as a result of an operation.

        public void EnterQueryExpression(QueryParser.QueryExpressionContext context)
        {
        }

        public void ExitQueryExpression(QueryParser.QueryExpressionContext context)
        {
            var operators = context.UNION_EXCEPT();

            CompileLeftAssociativeOperator(operators, _queries, (op, left, right) =>
            {
                if (string.Equals(op.Symbol.Text, "union", StringComparison.OrdinalIgnoreCase))
                {
                    var union = left.Union(right);
                    return union;
                }

                Trace.Assert(string.Equals(
                    op.Symbol.Text, 
                    "except", 
                    StringComparison.OrdinalIgnoreCase
                ), $"Expecting UNION or EXCEPT, got {op.Symbol.Text}");

                var except = left.Except(right);
                return except;
            });
        }

        public void EnterIntersection(QueryParser.IntersectionContext context)
        {
        }

        public void ExitIntersection(QueryParser.IntersectionContext context)
        {
            CompileLeftAssociativeOperator(
                context.INTERSECT(),
                _queries, 
                (op, left, right) => left.Intersect(right));
        }

        public void EnterQueryFactor(QueryParser.QueryFactorContext context)
        {
            if (context.ChildCount <= 0)
            {
                var symbol = context.Start;
                ReportError(
                    symbol.Line,
                    symbol.Column,
                    $"Missing subquery.");
            }
        }

        public void ExitQueryFactor(QueryParser.QueryFactorContext context)
        {
        }

        #endregion

        #region Simple query (SELECT, WHERE, ORDER BY, GROUP BY)

        // All methods in this group are either no-ops or they pop one query from the _queries
        // stack, transform it and push the transfromed query back to the _queries stack.
        // ExitSource is a source of queries. It only pushes 1 query to the _queries stack.
        // No queries will be removed from the stack in this method.

        public void EnterQuery(QueryParser.QueryContext context)
        {
        }

        public void ExitQuery(QueryParser.QueryContext context)
        {
        }

        public void EnterUnorderedQuery(QueryParser.UnorderedQueryContext context)
        {
        }

        public void ExitUnorderedQuery(QueryParser.UnorderedQueryContext context)
        {
        }

        public void EnterSource(QueryParser.SourceContext context)
        {
        }

        public void ExitSource(QueryParser.SourceContext context)
        {
            string viewIdentifier = null;
            var viewIdentifierToken = context.ID();
            if (viewIdentifierToken != null)
            {
                viewIdentifier = viewIdentifierToken.Symbol.Text;
            }
            else
            {
                viewIdentifierToken = context.COMPLEX_ID();
                if (viewIdentifierToken != null)
                {
                    viewIdentifier = ParseComplexIdentifier(viewIdentifierToken.Symbol);
                }
            }

            // create a query SELECT view
            if (viewIdentifier != null)
            {
                // if there is a cycle, report it and halt
                if (ReportCycle(
                        viewIdentifierToken.Symbol.Line, 
                        viewIdentifierToken.Symbol.Column, 
                        viewIdentifier))
                {
                    return;
                }

                // otherwise, compile the view
                var view = _compiler.Views.Find(viewIdentifier);
                if (view == null)
                {
                    ReportError(
                        viewIdentifierToken.Symbol.Line, 
                        viewIdentifierToken.Symbol.Column,
                        $"Unknown view '{viewIdentifier}'");
                    return;
                }

                // compile view
                var query = _compiler.Compile(
                    new StringReader(view.Text),
                    _errorListener, 
                    _parameters.Create(view.Name)) as IQuery;

                if (query == null) // compilation of the view failed
                {
                    HaltCompilation();
                    return;
                }

                _queries.Push(query.View(viewIdentifier));
            }
            else if (context.STRING() != null) // create a query SELECT pattern
            {
                var patternSymbol = context.STRING().Symbol;
                var pattern = ParseStringValue(patternSymbol);

                try
                {
                    var query = _queryFactory.CreateQuery(pattern) as IQuery;
                    _queries.Push(query);
                }
                catch (ArgumentException) // invalid pattern
                {
                    ReportError(
                        patternSymbol.Line, 
                        patternSymbol.Column, 
                        $"Invalid path pattern: {pattern}");
                }
            }

            // otherwise, this is a subquery => it will push its own tree to the stack
        }

        public void EnterOptionalWhere(QueryParser.OptionalWhereContext context)
        {
        }

        public void ExitOptionalWhere(QueryParser.OptionalWhereContext context)
        {
            if (context.WHERE() != null)
            {
                if (_expressions.Count <= 0)
                {
                    // the predicate is invalid and we can't compile any part of it
                    return; 
                }

                var query = _queries.Pop();
                var predicate = _expressions.Pop();
                _queries.Push(query.Where(predicate));
            }
        }

        public void EnterOptionalOrderBy(QueryParser.OptionalOrderByContext context)
        {
        }

        public void ExitOptionalOrderBy(QueryParser.OptionalOrderByContext context)
        {
            if (context.ORDER() != null)
            {
                var query = _queries.Pop();
                var comparer = _comparers.Pop();

                var startIndex = context.BY().Symbol.StopIndex + 1;
                var endIndex = context.Stop.StopIndex;
                var text = context.Stop.InputStream
                    .GetText(new Interval(startIndex, endIndex))
                    .Trim();
                _queries.Push(query.WithComparer(comparer, text));
            }
        }

        public void EnterOrderByList(QueryParser.OrderByListContext context)
        {
        }

        public void ExitOrderByList(QueryParser.OrderByListContext context)
        {
            CompileLeftAssociativeOperator(
                context.PARAM_DELIMITER(), 
                _comparers, 
                (_, left, right) => new EntityComparer(left, right));
        }
        
        public void EnterOrderByKey(QueryParser.OrderByKeyContext context)
        {
        }

        public void ExitOrderByKey(QueryParser.OrderByKeyContext context)
        {
            // parse direction
            var sortDirection = 1;
            var directionString = context.DIRECTION()?.Symbol.Text;
            if (string.Equals(directionString, "desc", StringComparison.OrdinalIgnoreCase))
            {
                sortDirection = -1;
            }

            // parse sort key
            var valueExpression = _expressions.Pop();
            var key = new SortParameter
            {
                Direction = sortDirection,
                Getter = valueExpression.CompileFunction(_runtime)
            };

            _comparers.Push(new EntityComparer(new List<SortParameter>{ key }));
        }

        public void EnterOptionalGroupBy(QueryParser.OptionalGroupByContext context)
        {
        }

        public void ExitOptionalGroupBy(QueryParser.OptionalGroupByContext context)
        {
            if (context.GROUP() == null)
            {
                return; // there is no GROUP BY clause
            }

            var query = _queries.Pop();
            var expression = _expressions.Pop();
            query = query.WithGroup(expression);
            _queries.Push(query);
        }

        #endregion

        #region Value expression

        // Methods in this group are either no-ops or they pop 1 or more expressions from the
        // _expression stack and push 1 expression as a result of some function on the removed
        // subexpressions. The ExitFactor method is a soruce of expressions. It pushes 1
        // expression to the _expressions stack. No expressions will be removed by this method.

        public void EnterPredicate(QueryParser.PredicateContext context)
        {
        }

        public void ExitPredicate(QueryParser.PredicateContext context)
        {
            CompileLeftAssociativeOperator(context.OR(), _expressions, 
                (op, left, right) => 
                    new OrExpression(op.Symbol.Line, op.Symbol.Column, left, right));
        }
        
        public void EnterConjunction(QueryParser.ConjunctionContext context)
        {
        }

        public void ExitConjunction(QueryParser.ConjunctionContext context)
        {
            CompileLeftAssociativeOperator(context.AND(), _expressions,
                (op, left, right) =>
                    new AndExpression(op.Symbol.Line, op.Symbol.Column, left, right));
        }
        
        public void EnterLiteral(QueryParser.LiteralContext context)
        {
        }

        public void ExitLiteral(QueryParser.LiteralContext context)
        {
            var op = context.NOT();
            if (op == null)
            {
                return;
            }
            
            var expr = _expressions.Pop();
            _expressions.Push(new NotExpression(op.Symbol.Line, op.Symbol.Column, expr));
        }

        public void EnterComparison(QueryParser.ComparisonContext context)
        {
        }

        public void ExitComparison(QueryParser.ComparisonContext context)
        {
            var opToken = context.REL_OP();
            if (opToken == null)
            {
                return;
            }
            
            var op = opToken.Symbol;
            var right = _expressions.Pop();
            var left = _expressions.Pop();

            BinaryOperatorExpression value = null;
            switch (op.Text)
            {
                case "<":
                    value = new LessThanOperator(op.Line, op.Column, left, right);
                    break;
                case "<=":
                    value = new LessThanOrEqualOperator(op.Line, op.Column, left, right);
                    break;
                case "<>":
                case "!=":
                    value = new NotEqualOperator(op.Line, op.Column, left, right);
                    break;
                case "==":
                case "=":
                    value = new EqualOperator(op.Line, op.Column, left, right);
                    break;
                case ">":
                    value = new GreaterThanOperator(op.Line, op.Column, left, right);
                    break;
                case ">=":
                    value = new GreaterThanOrEqualOperator(op.Line, op.Column, left, right);
                    break;
            }

            Trace.Assert(value != null, "Invalid comparison operator " + op.Text);

            _expressions.Push(value);
        }

        public void EnterExpression(QueryParser.ExpressionContext context)
        {
        }

        public void ExitExpression(QueryParser.ExpressionContext context)
        {
            CompileLeftAssociativeOperator(context.ADD_SUB(), _expressions, 
                (opNode, left, right) =>
                {
                    var op = opNode.Symbol;
                    BinaryOperatorExpression value = null;
                    switch (op.Text)
                    {
                        case "+":
                            value = new AdditionExpression(op.Line, op.Column, left, right);
                            break;
                        case "-":
                            value = new SubtractionExpression(op.Line, op.Column, left, right);
                            break;
                    }

                    Trace.Assert(value != null, "Invalid addition operator " + op.Text);
                    return value;
                });
        }
        
        public void EnterMultiplication(QueryParser.MultiplicationContext context)
        {
        }

        public void ExitMultiplication(QueryParser.MultiplicationContext context)
        {
            CompileLeftAssociativeOperator(context.MULT_DIV(), _expressions,
                (opNode, left, right) =>
                {
                    var op = opNode.Symbol;
                    BinaryOperatorExpression value = null;
                    switch (op.Text)
                    {
                        case "*":
                            value = new MultiplicationExpression(op.Line, op.Column, left, right);
                            break;
                        case "/":
                            value = new DivisionExpression(op.Line, op.Column, left, right);
                            break;
                    }

                    Trace.Assert(value != null, "Invalid multiplication operator " + op.Text);

                    return value;
                });
        }
        
        public void EnterFactor(QueryParser.FactorContext context)
        {
        }

        public void ExitFactor(QueryParser.FactorContext context)
        {
            BaseValue constantValue = null;

            // parse INT
            var constantToken = context.INT();
            if (constantToken != null)
            {
                var value = int.Parse(constantToken.Symbol.Text, CultureInfo.InvariantCulture);
                constantValue = new IntValue(value);
            }
            else if (context.REAL() != null) // parse REAL
            {
                constantToken = context.REAL();
                var value = double.Parse(constantToken.Symbol.Text, CultureInfo.InvariantCulture);
                constantValue = new RealValue(value);
            }
            else if (context.STRING() != null) // parse STRING
            {
                constantToken = context.STRING();
                constantValue = new StringValue(ParseStringValue(constantToken.Symbol));
            }
            
            // if this is a constant
            if (constantValue != null)
            {
                _expressions.Push(new ConstantExpression(
                    constantToken.Symbol.Line, 
                    constantToken.Symbol.Column, 
                    constantValue));
                return;
            }
            
            // parse ID
            string identifier = null;
            var identifierToken = context.ID();
            if (identifierToken != null)
            {
                identifier = identifierToken.Symbol.Text;
            }
            else if (context.COMPLEX_ID() != null) // parse COMPLEX_ID
            {
                identifierToken = context.COMPLEX_ID();
                identifier = ParseComplexIdentifier(identifierToken.Symbol);
            }
            
            // this is an ID or a function call
            if (identifierToken != null)
            {
                if (context.LPAREN() == null) // attribute access
                {
                    _expressions.Push(new AttributeAccessExpression(
                        identifierToken.Symbol.Line,
                        identifierToken.Symbol.Column,
                        identifier));
                }
                else // function call
                {
                    var stackTop = _expressionsFrameStart.Pop();
                    var parameters = new List<ValueExpression>();
                    while (_expressions.Count > stackTop)
                    {
                        parameters.Add(_expressions.Pop());
                    }

                    parameters.Reverse();
                    _expressions.Push(new FunctionCallExpression(
                        identifierToken.Symbol.Line,
                        identifierToken.Symbol.Column,
                        identifier,
                        parameters));
                }

                return;
            }

            // if this is a unary minus
            var unaryOperator = context.ADD_SUB();
            if (unaryOperator != null)
            {
                if (unaryOperator.Symbol.Text == "-")
                {
                    var parameter = _expressions.Pop();
                    _expressions.Push(new UnaryMinusExpression(
                        unaryOperator.Symbol.Line,
                        unaryOperator.Symbol.Column,
                        parameter));
                }
            }

            // otherwise, this is a subexpression => it is already on the stack
        }

        /// <summary>
        /// In this method we remember the "return address" of a function. That is, a place
        /// in the _expressions stack where we should return after the function call. To put
        /// it in another way, it is the index of the first argument of this function in the stack.
        /// </summary>
        /// <param name="context"></param>
        public void EnterArgumentList(QueryParser.ArgumentListContext context)
        {
            _expressionsFrameStart.Push(_expressions.Count);
        }

        public void ExitArgumentList(QueryParser.ArgumentListContext context)
        {
        }

        #endregion

        private string ParseStringValue(IToken token)
        {
            if (token.Type != QueryLexer.STRING)
                throw new ArgumentOutOfRangeException(nameof(token));
            return ParseBoundedValue(token, '"');
        }

        private string ParseComplexIdentifier(IToken token)
        {
            if (token.Type != QueryLexer.COMPLEX_ID)
                throw new ArgumentOutOfRangeException(nameof(token));
            return ParseBoundedValue(token, '`');
        }

        /// <summary>
        /// Parse string value of <paramref name="token"/> which should be bounded between
        /// 2 characters (<paramref name="bound"/>). The token value is required to start
        /// with <paramref name="bound"/> but it doesn't have to end with <paramref name="bound"/>.
        /// If it ends with a new line character, <paramref name="bound"/> or EOF, it will be
        /// trimmed.
        /// </summary>
        /// <remarks>
        /// If <paramref name="token"/> is not properly terminated with the <paramref name="bound"/>
        /// character, a compilation error will be reported but a value will still be returned.
        /// </remarks>
        /// <param name="token"></param>
        /// <param name="bound"></param>
        /// <returns></returns>
        private string ParseBoundedValue(IToken token, char bound)
        {
            if (token.Text.Length <= 0 || token.Text[0] != bound)
                throw new ArgumentOutOfRangeException(nameof(token));

            // number of characters to remove from the start and from the end
            int trimStart = 1;
            var trimEnd = 0;

            // remove the end character if the value is terminated (it won't be terminated iff
            // we have reached the EOF)
            var lastCharacter = token.Text[token.Text.Length - 1];
            if (token.Text.Length > 1 && (
                    lastCharacter == bound ||
                    lastCharacter == '\n' ||
                    lastCharacter == '\r'))
            {
                trimEnd = 1;
            }

            // check if the token is terminated correctly
            if (token.Text.Length <= 1 || token.Text[token.Text.Length - 1] != bound)
            {
                _errorListener.OnCompilerError(
                    token.Line, 
                    token.Column, 
                    $"Unterminated {QueryLexer.DefaultVocabulary.GetDisplayName(token.Type)}");
            }

            return token.Text.Substring(trimStart, token.Text.Length - trimStart - trimEnd);
        }
        
        private void ReportError(int line, int column, string message)
        {
            _errorListener.OnCompilerError(line, column, message);
            HaltCompilation();
        }

        private bool ReportCycle(int line, int column, string viewName)
        {
            if (!_parameters.Parent.ContainsKey(viewName))
            {
                return false;
            }

            // fetch query view names in the cycle and format an error
            var name = viewName;
            var sb = new StringBuilder();
            sb.Append("Query view cycle detected (");

            var path = new List<string> { viewName };
            while (_parameters.Parent.TryGetValue(name, out var parent))
            {
                if (name == parent)
                    break; // self cycle
                path.Add(parent);
                name = parent;
            }
            
            sb.Append(path[path.Count - 1]);
            foreach (var element in path)
            {
                sb.Append(" -> ");
                sb.Append(element);
            }

            sb.Append(')');
            
            ReportError(line, column, sb.ToString());

            return true;
        }

        private void HaltCompilation()
        {
            throw new ParseCanceledException();
        }
    }
    
    internal class ParserErrorListener : IAntlrErrorListener<IToken>
    {
        private readonly IQueryErrorListener _queryErrorListener;
        
        public ParserErrorListener(IQueryErrorListener listener)
        {
            _queryErrorListener = listener;
        }

        public void SyntaxError(
            TextWriter output, 
            IRecognizer recognizer, 
            IToken offendingSymbol, 
            int line, 
            int charPositionInLine,
            string msg, 
            RecognitionException e)
        {
            _queryErrorListener.OnCompilerError(line, charPositionInLine, msg);
            throw new ParseCanceledException(e);
        }
    }

    internal class LexerErrorListener : IAntlrErrorListener<int>
    {
        private readonly IQueryErrorListener _queryErrorListener;

        public LexerErrorListener(IQueryErrorListener listener)
        {
            _queryErrorListener = listener;
        }

        public void SyntaxError(
            TextWriter output,
            IRecognizer recognizer, 
            int offendingSymbol, 
            int line, 
            int charPositionInLine,
            string msg, 
            RecognitionException e)
        {
            _queryErrorListener.OnCompilerError(line, charPositionInLine, msg);
        }
    }

    internal class QueryCompilationParameters
    {
        /// <summary>
        /// Name of the query view which is currently being compiled.
        /// </summary>
        public string ViewName { get; private set; }

        /// <summary>
        /// Map query view dependencies 
        /// </summary>
        public Dictionary<string, string> Parent { get; private set; } = 
            new Dictionary<string, string>();

        /// <summary>
        /// true iff this is a recursive compilation call
        /// </summary>
        public bool IsRecursiveCall => ViewName != null;
        
        /// <summary>
        /// Create a new parameters for recursive compilation of <paramref name="newViewName"/>
        /// </summary>
        /// <param name="newViewName">
        /// Name of the view which will be compiled using these parameters.
        /// </param>
        /// <returns></returns>
        public QueryCompilationParameters Create(string newViewName)
        {
            if (ViewName != null)
            {
                Parent[ViewName] = newViewName;
            }

            var result = new QueryCompilationParameters
            {
                ViewName = newViewName,
                Parent = Parent
            };
            return result;
        }
    }
    
    [Export(typeof(IQueryCompiler))]
    public class QueryCompiler : IQueryCompiler
    {
        private readonly IQueryErrorListener _queryQueryErrorListener;
        private readonly IQueryFactory _queryFactory;
        private readonly IRuntime _runtime;

        public IQueryViewRepository Views { get; }

        [ImportingConstructor]
        public QueryCompiler(
            IQueryFactory queryFactory, 
            IRuntime runtime, 
            IQueryViewRepository queryViewRepository,
            [Import(typeof(AggregateQueryErrorListener))] IQueryErrorListener queryErrorListener)
        {
            _queryFactory = queryFactory;
            _runtime = runtime;
            _queryQueryErrorListener = queryErrorListener;
            Views = queryViewRepository;
        }

        public IExecutableQuery Compile(TextReader reader, IQueryErrorListener listener)
        {
            return Compile(reader, listener, new QueryCompilationParameters());
        }
        
        internal IExecutableQuery Compile(
            TextReader inputQuery, 
            IQueryErrorListener queryErrorListener,
            QueryCompilationParameters parameters)
        {
            // create all necessary components to parse a query
            var input = new AntlrInputStream(inputQuery);
            var lexer = new QueryLexer(input);
            lexer.AddErrorListener(new LexerErrorListener(queryErrorListener));

            var compiler = new QueryCompilationListener(
                _queryFactory,
                this,
                queryErrorListener,
                _runtime,
                parameters);

            var tokenStream = new CommonTokenStream(lexer);
            var errorListener = new ParserErrorListener(queryErrorListener);
            var parser = new QueryParser(tokenStream);
            parser.AddErrorListener(errorListener);

            // parse and compile the query
            if (!parameters.IsRecursiveCall)
            {
                queryErrorListener.BeforeCompilation();
            }

            IQuery result;
            try
            {
                var tree = parser.entry();
                ParseTreeWalker.Default.Walk(compiler, tree);
                result = compiler.Finish();

                if (result == null)
                {
                    return null;
                }

                result = result.WithText(input.ToString());
            }
            catch (ParseCanceledException)
            {
                // an error has already been reported 
                return null;
            }
            finally
            {
                if (!parameters.IsRecursiveCall)
                {
                    queryErrorListener.AfterCompilation();
                }
            }

            return result;
        }

        public IExecutableQuery Compile(string query)
        {
            return Compile(new StringReader(query), _queryQueryErrorListener);
        }
    }
}
