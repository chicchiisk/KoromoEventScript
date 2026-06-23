using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Lexing;
using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Semantics;

public sealed class TypeChecker
{
    private readonly BuiltInSignatureRegistry builtIns;

    public TypeChecker()
        : this(new BuiltInSignatureRegistry())
    {
    }

    public TypeChecker(BuiltInSignatureRegistry builtIns)
    {
        ArgumentNullException.ThrowIfNull(builtIns);
        this.builtIns = builtIns;
    }

    public TypeCheckingResult CheckTypes(
        ImportGraph graph,
        IReadOnlyList<DefinitionCollectionResult> definitionCollections)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(definitionCollections);

        var context = TypeCheckingContext.From(definitionCollections, builtIns);
        var diagnostics = new List<Diagnostic>();

        foreach (var document in graph.OrderedDocuments)
        {
            if (!context.DocumentsByModule.TryGetValue(document.ModuleName, out var documentContext))
            {
                continue;
            }

            var checker = new DocumentChecker(documentContext, context, graph, diagnostics);
            checker.Check();
        }

        return diagnostics.Count == 0
            ? TypeCheckingResult.Success()
            : TypeCheckingResult.Failure(diagnostics);
    }

    private static KesType ParseType(IReadOnlyList<Token> tokens)
    {
        if (tokens.Count == 0)
        {
            return KesType.Void;
        }

        var index = 0;
        var baseType = tokens[index].Lexeme switch
        {
            "number" => KesType.Number,
            "bool" => KesType.Bool,
            "string" => KesType.String,
            "Actor" => KesType.Actor,
            "void" => KesType.Void,
            _ => KesType.Unsupported,
        };

        index++;
        while (index + 1 < tokens.Count &&
               tokens[index].Kind == TokenKind.OpenBracket &&
               tokens[index + 1].Kind == TokenKind.CloseBracket)
        {
            baseType = baseType.Kind == KesTypeKind.Unsupported ? KesType.Unsupported : KesType.Array(baseType);
            index += 2;
        }

        return index == tokens.Count ? baseType : KesType.Unsupported;
    }

    private static Diagnostic Diagnostic(string file, SourceLocation location, string message)
    {
        return new Diagnostic(DiagnosticLevel.Error, "KES2015", file, location.Line, location.Column, message);
    }

    private sealed class DocumentChecker
    {
        private readonly DocumentTypeContext document;
        private readonly TypeCheckingContext context;
        private readonly ImportGraph graph;
        private readonly List<Diagnostic> diagnostics;
        private readonly Stack<Dictionary<string, KesType>> locals = new();

        public DocumentChecker(
            DocumentTypeContext document,
            TypeCheckingContext context,
            ImportGraph graph,
            List<Diagnostic> diagnostics)
        {
            this.document = document;
            this.context = context;
            this.graph = graph;
            this.diagnostics = diagnostics;
        }

        public void Check()
        {
            locals.Push(new Dictionary<string, KesType>(document.ModuleVariables, StringComparer.Ordinal));
            foreach (var statement in document.Document.Syntax.Statements)
            {
                CheckStatement(statement);
            }

            locals.Pop();
        }

        private void CheckBlock(BlockSyntax block)
        {
            locals.Push(new Dictionary<string, KesType>(StringComparer.Ordinal));
            foreach (var statement in block.Statements)
            {
                CheckStatement(statement);
            }

            locals.Pop();
        }

        private void CheckStatement(StatementSyntax statement)
        {
            switch (statement)
            {
                case VarStatementSyntax varStatement:
                    CheckVar(varStatement);
                    break;

                case AssignmentStatementSyntax assignment:
                    CheckAssignment(assignment);
                    break;

                case FunctionDeclarationSyntax function:
                    CheckFunction(function);
                    break;

                case ActorDeclarationSyntax actor:
                    foreach (var actorStatement in actor.Body.Statements)
                    {
                        if (actorStatement is not VarStatementSyntax)
                        {
                            diagnostics.Add(Diagnostic(document.Document.ProjectRelativePath, GetStatementLocation(actorStatement), "Actor declarations may only contain var statements."));
                            continue;
                        }

                        CheckStatement(actorStatement);
                    }
                    break;

                case StandbyStatementSyntax standby:
                    foreach (var entry in standby.Entries)
                    {
                        locals.Peek()[entry.InstanceName] = KesType.Actor;
                    }

                    break;

                case ClassDeclarationSyntax classDeclaration:
                    foreach (var member in classDeclaration.Members)
                    {
                        if (member is ClassFieldSyntax field)
                        {
                            CheckVar(field.Declaration);
                        }
                        else if (member is ClassMethodSyntax method)
                        {
                            CheckFunction(method.Declaration);
                        }
                    }

                    break;

                case CommandStatementSyntax command:
                    CheckCall(command.Name, command.Arguments, command.NameLocation, requireValue: false);
                    break;

                case LessStatementSyntax less:
                    CheckLess(less);
                    break;

                case SayStatementSyntax say:
                    var speakerType = ResolveValue(say.Speaker);
                    RequireAssignable(KesType.Actor, speakerType, say.SpeakerLocation, $"Expected say speaker '{say.Speaker}' to be Actor.");
                    break;

                case IfStatementSyntax ifStatement:
                    RequireAssignable(KesType.Bool, Evaluate(ifStatement.ConditionTokens, requireValue: true), ifStatement.IfLocation, "If condition must be bool.");
                    CheckBlock(ifStatement.Body);
                    foreach (var elseIfClause in ifStatement.ElseIfClauses)
                    {
                        RequireAssignable(KesType.Bool, Evaluate(elseIfClause.ConditionTokens, requireValue: true), elseIfClause.ElseIfLocation, "Else-if condition must be bool.");
                        CheckBlock(elseIfClause.Body);
                    }

                    if (ifStatement.ElseBody is not null)
                    {
                        CheckBlock(ifStatement.ElseBody);
                    }

                    break;

                case WhileStatementSyntax whileStatement:
                    RequireAssignable(KesType.Bool, Evaluate(whileStatement.ConditionTokens, requireValue: true), whileStatement.WhileLocation, "While condition must be bool.");
                    CheckBlock(whileStatement.Body);
                    break;

                case ForStatementSyntax forStatement:
                    var iterableType = Evaluate(forStatement.IterableTokens, requireValue: true);
                    if (iterableType.Kind != KesTypeKind.Unknown && iterableType.Kind != KesTypeKind.Array)
                    {
                        diagnostics.Add(Diagnostic(document.Document.ProjectRelativePath, forStatement.ForLocation, $"For iterable must be an array, but got {iterableType}."));
                    }

                    locals.Push(new Dictionary<string, KesType>(StringComparer.Ordinal)
                    {
                        [forStatement.VariableName] = iterableType.Kind == KesTypeKind.Array ? iterableType.ElementType! : KesType.Unknown,
                    });
                    foreach (var bodyStatement in forStatement.Body.Statements)
                    {
                        CheckStatement(bodyStatement);
                    }

                    locals.Pop();
                    break;
            }
        }

        private void CheckVar(VarStatementSyntax varStatement)
        {
            var annotatedType = varStatement.TypeTokens.Count == 0 ? null : ParseType(varStatement.TypeTokens);
            if (annotatedType?.Kind == KesTypeKind.Unsupported)
            {
                diagnostics.Add(Diagnostic(document.Document.ProjectRelativePath, varStatement.NameLocation, $"Unsupported or unknown type annotation for '{varStatement.Name}'."));
                annotatedType = KesType.Unknown;
            }

            var initializerType = varStatement.ValueTokens.Count == 0 ? null : Evaluate(varStatement.ValueTokens, requireValue: true);
            var variableType = annotatedType ?? initializerType ?? KesType.Unknown;
            if (annotatedType is not null && initializerType is not null)
            {
                RequireAssignable(annotatedType, initializerType, varStatement.NameLocation, $"Cannot assign {initializerType} to {annotatedType} variable '{varStatement.Name}'.");
            }

            locals.Peek()[varStatement.Name] = variableType;
        }

        private void CheckAssignment(AssignmentStatementSyntax assignment)
        {
            var targetType = ResolveValue(assignment.TargetName);
            var valueType = Evaluate(assignment.ValueTokens, requireValue: true);
            RequireAssignable(targetType, valueType, assignment.TargetLocation, $"Cannot assign {valueType} to {targetType} variable '{assignment.TargetName}'.");
        }

        private void CheckFunction(FunctionDeclarationSyntax function)
        {
            locals.Push(new Dictionary<string, KesType>(StringComparer.Ordinal));
            foreach (var parameter in function.Parameters)
            {
                var parameterType = ParseType(parameter.TypeTokens);
                if (parameterType.Kind == KesTypeKind.Unsupported)
                {
                    diagnostics.Add(Diagnostic(document.Document.ProjectRelativePath, parameter.NameLocation, $"Unsupported or unknown type annotation for '{parameter.Name}'."));
                    parameterType = KesType.Unknown;
                }

                locals.Peek()[parameter.Name] = parameterType;
            }

            foreach (var statement in function.Body.Statements)
            {
                CheckStatement(statement);
            }

            locals.Pop();
        }

        private void CheckLess(LessStatementSyntax less)
        {
            foreach (var item in less.Items)
            {
                switch (item)
                {
                    case LessCommandItemSyntax commandItem:
                        CheckCall(less.Name, less.SharedArguments.Concat(commandItem.Arguments).ToArray(), less.NameLocation, requireValue: false);
                        break;

                    case LessNestedStatementSyntax nested:
                        CheckLess(nested.Statement);
                        break;
                }
            }
        }

        private KesType CheckCall(string name, IReadOnlyList<Token> argumentTokens, SourceLocation location, bool requireValue)
        {
            var signature = ResolveCallable(name);
            if (signature is null)
            {
                return KesType.Unknown;
            }

            var arguments = SplitArguments(argumentTokens);
            var usedParameters = new HashSet<string>(StringComparer.Ordinal);
            var positionalIndex = 0;

            foreach (var argument in arguments)
            {
                CallableParameter? parameter = null;
                if (argument.Name is not null)
                {
                    parameter = signature.Parameters.FirstOrDefault(candidate => string.Equals(candidate.Name, argument.Name, StringComparison.Ordinal));
                }
                else if (positionalIndex < signature.Parameters.Count)
                {
                    parameter = signature.Parameters[positionalIndex++];
                }

                if (parameter is null)
                {
                    diagnostics.Add(Diagnostic(document.Document.ProjectRelativePath, argument.Location, $"Unexpected argument for '{name}'."));
                    continue;
                }

                usedParameters.Add(parameter.Name);
                var actualType = Evaluate(argument.Tokens, requireValue: true);
                if (signature.AcceptsAnyArray && parameter.Type.Kind == KesTypeKind.Array)
                {
                    if (actualType.Kind is not (KesTypeKind.Array or KesTypeKind.Unknown))
                    {
                        diagnostics.Add(Diagnostic(document.Document.ProjectRelativePath, argument.Location, $"Argument '{parameter.Name}' for '{name}' must be an array, but got {actualType}."));
                    }
                }
                else
                {
                    RequireAssignable(parameter.Type, actualType, argument.Location, $"Argument '{parameter.Name}' for '{name}' expects {parameter.Type}, but got {actualType}.");
                }
            }

            foreach (var parameter in signature.Parameters.Where(parameter => !parameter.IsOptional && !usedParameters.Contains(parameter.Name)))
            {
                diagnostics.Add(Diagnostic(document.Document.ProjectRelativePath, location, $"Missing required argument '{parameter.Name}' for '{name}'."));
            }

            if (requireValue && signature.ReturnType.Kind == KesTypeKind.Void)
            {
                diagnostics.Add(Diagnostic(document.Document.ProjectRelativePath, location, $"Call '{name}' returns void and cannot be used as a value."));
            }

            return signature.ReturnType;
        }

        private CallableSignature? ResolveCallable(string name)
        {
            if (document.Callables.TryGetValue(name, out var local))
            {
                return local;
            }

            foreach (var moduleName in graph.GetReachableImports(document.Document.ModuleName))
            {
                if (context.DocumentsByModule.TryGetValue(moduleName, out var imported) &&
                    imported.Callables.TryGetValue(name, out var importedCallable))
                {
                    return importedCallable;
                }
            }

            return context.BuiltIns.TryResolve(name, out var builtIn) ? builtIn : null;
        }

        private KesType Evaluate(IReadOnlyList<Token> tokens, bool requireValue)
        {
            if (tokens.Count == 0)
            {
                return KesType.Unknown;
            }

            var parser = new ExpressionParser(this, tokens, requireValue);
            return parser.ParseExpression();
        }

        private KesType ResolveValue(string name)
        {
            foreach (var scope in locals)
            {
                if (scope.TryGetValue(name, out var type))
                {
                    return type;
                }
            }

            if (document.ModuleVariables.TryGetValue(name, out var moduleType))
            {
                return moduleType;
            }

            foreach (var moduleName in graph.GetReachableImports(document.Document.ModuleName))
            {
                if (context.DocumentsByModule.TryGetValue(moduleName, out var imported) &&
                    imported.ModuleVariables.TryGetValue(name, out var importedType))
                {
                    return importedType;
                }
            }

            return KesType.Unknown;
        }

        private void RequireAssignable(KesType expected, KesType actual, SourceLocation location, string message)
        {
            if (expected.Kind == KesTypeKind.Unknown || actual.Kind == KesTypeKind.Unknown)
            {
                return;
            }

            if (!expected.IsAssignableFrom(actual))
            {
                diagnostics.Add(Diagnostic(document.Document.ProjectRelativePath, location, message));
            }
        }

        private static SourceLocation GetStatementLocation(StatementSyntax statement)
        {
            return statement switch
            {
                VarStatementSyntax varStatement => varStatement.NameLocation,
                AssignmentStatementSyntax assignment => assignment.TargetLocation,
                FunctionDeclarationSyntax function => function.NameLocation,
                ActorDeclarationSyntax actor => actor.NameLocation,
                StandbyStatementSyntax standby => standby.KeywordLocation,
                EnumDeclarationSyntax @enum => @enum.NameLocation,
                ClassDeclarationSyntax @class => @class.NameLocation,
                LabelStatementSyntax label => label.TagLocation,
                JumpStatementSyntax jump => jump.TagLocation,
                CommandStatementSyntax command => command.NameLocation,
                LessStatementSyntax less => less.NameLocation,
                SayStatementSyntax say => say.SpeakerLocation,
                NarStatementSyntax nar => nar.TagLocation ?? nar.KeywordLocation,
                SelectStatementSyntax select => select.TagLocation ?? select.KeywordLocation,
                IfStatementSyntax ifStatement => ifStatement.IfLocation,
                WhileStatementSyntax whileStatement => whileStatement.WhileLocation,
                ForStatementSyntax forStatement => forStatement.ForLocation,
                _ => new SourceLocation(1, 1),
            };
        }

        private static IReadOnlyList<CallArgument> SplitArguments(IReadOnlyList<Token> tokens)
        {
            var arguments = new List<CallArgument>();
            var index = 0;
            while (index < tokens.Count)
            {
                string? name = null;
                if (index + 1 < tokens.Count &&
                    tokens[index].Kind is TokenKind.Identifier or TokenKind.Keyword &&
                    tokens[index + 1].Kind == TokenKind.Equals)
                {
                    name = tokens[index].Lexeme;
                    index += 2;
                }

                var start = index;
                var depth = 0;
                if (index < tokens.Count && tokens[index].Kind is TokenKind.OpenParen or TokenKind.OpenBracket)
                {
                    depth = 1;
                    var open = tokens[index].Kind;
                    var close = open == TokenKind.OpenParen ? TokenKind.CloseParen : TokenKind.CloseBracket;
                    index++;
                    while (index < tokens.Count && depth > 0)
                    {
                        if (tokens[index].Kind == open)
                        {
                            depth++;
                        }
                        else if (tokens[index].Kind == close)
                        {
                            depth--;
                        }

                        index++;
                    }
                }
                else if (index + 1 < tokens.Count && tokens[index].Kind is TokenKind.Plus or TokenKind.Minus && tokens[index + 1].Kind == TokenKind.NumberLiteral)
                {
                    index += 2;
                }
                else
                {
                    index++;
                }

                if (start < tokens.Count)
                {
                    var argumentTokens = tokens.Skip(start).Take(index - start).ToArray();
                    arguments.Add(new CallArgument(name, argumentTokens, new SourceLocation(argumentTokens[0].Line, argumentTokens[0].Column)));
                }
            }

            return arguments;
        }

        private sealed record CallArgument(string? Name, IReadOnlyList<Token> Tokens, SourceLocation Location);

        private sealed class ExpressionParser
        {
            private readonly DocumentChecker checker;
            private readonly IReadOnlyList<Token> tokens;
            private readonly bool requireValue;
            private int position;

            public ExpressionParser(DocumentChecker checker, IReadOnlyList<Token> tokens, bool requireValue)
            {
                this.checker = checker;
                this.tokens = tokens;
                this.requireValue = requireValue;
            }

            public KesType ParseExpression()
            {
                return ParseLogicalOr();
            }

            private KesType ParseLogicalOr()
            {
                var left = ParseLogicalAnd();
                while (Match(TokenKind.OrOr))
                {
                    var op = Previous;
                    var right = ParseLogicalAnd();
                    checker.RequireAssignable(KesType.Bool, left, Location(op), "Logical operator requires bool operands.");
                    checker.RequireAssignable(KesType.Bool, right, Location(op), "Logical operator requires bool operands.");
                    left = KesType.Bool;
                }

                return left;
            }

            private KesType ParseLogicalAnd()
            {
                var left = ParseEquality();
                while (Match(TokenKind.AndAnd))
                {
                    var op = Previous;
                    var right = ParseEquality();
                    checker.RequireAssignable(KesType.Bool, left, Location(op), "Logical operator requires bool operands.");
                    checker.RequireAssignable(KesType.Bool, right, Location(op), "Logical operator requires bool operands.");
                    left = KesType.Bool;
                }

                return left;
            }

            private KesType ParseEquality()
            {
                var left = ParseComparison();
                while (Match(TokenKind.DoubleEquals) || Match(TokenKind.NotEquals))
                {
                    var op = Previous;
                    var right = ParseComparison();
                    if (left.Kind != KesTypeKind.Unknown && right.Kind != KesTypeKind.Unknown &&
                        !left.IsAssignableFrom(right) && !right.IsAssignableFrom(left))
                    {
                        checker.diagnostics.Add(Diagnostic(checker.document.Document.ProjectRelativePath, Location(op), $"Cannot compare {left} and {right}."));
                    }

                    left = KesType.Bool;
                }

                return left;
            }

            private KesType ParseComparison()
            {
                var left = ParseTerm();
                while (Match(TokenKind.Less) || Match(TokenKind.LessOrEqual) || Match(TokenKind.Greater) || Match(TokenKind.GreaterOrEqual))
                {
                    var op = Previous;
                    var right = ParseTerm();
                    checker.RequireAssignable(KesType.Number, left, Location(op), "Comparison operator requires number operands.");
                    checker.RequireAssignable(KesType.Number, right, Location(op), "Comparison operator requires number operands.");
                    left = KesType.Bool;
                }

                return left;
            }

            private KesType ParseTerm()
            {
                var left = ParseFactor();
                while (Match(TokenKind.Plus) || Match(TokenKind.Minus))
                {
                    var op = Previous;
                    var right = ParseFactor();
                    checker.RequireAssignable(KesType.Number, left, Location(op), "Arithmetic operator requires number operands.");
                    checker.RequireAssignable(KesType.Number, right, Location(op), "Arithmetic operator requires number operands.");
                    left = KesType.Number;
                }

                return left;
            }

            private KesType ParseFactor()
            {
                var left = ParseUnary();
                while (Match(TokenKind.Star) || Match(TokenKind.Slash))
                {
                    var op = Previous;
                    var right = ParseUnary();
                    checker.RequireAssignable(KesType.Number, left, Location(op), "Arithmetic operator requires number operands.");
                    checker.RequireAssignable(KesType.Number, right, Location(op), "Arithmetic operator requires number operands.");
                    left = KesType.Number;
                }

                return left;
            }

            private KesType ParseUnary()
            {
                if (Match(TokenKind.Bang))
                {
                    var op = Previous;
                    var operand = ParseUnary();
                    checker.RequireAssignable(KesType.Bool, operand, Location(op), "Logical operator requires bool operand.");
                    return KesType.Bool;
                }

                if (Match(TokenKind.Plus) || Match(TokenKind.Minus))
                {
                    var op = Previous;
                    var operand = ParseUnary();
                    checker.RequireAssignable(KesType.Number, operand, Location(op), "Unary numeric operator requires number operand.");
                    return KesType.Number;
                }

                return ParsePostfix();
            }

            private KesType ParsePostfix()
            {
                var value = ParsePrimary();
                while (true)
                {
                    if (Match(TokenKind.Dot))
                    {
                        var dot = Previous;
                        var memberToken = AdvanceIf(TokenKind.Identifier) ?? AdvanceIf(TokenKind.Keyword);
                        if (memberToken is null)
                        {
                            checker.diagnostics.Add(Diagnostic(checker.document.Document.ProjectRelativePath, Location(dot), "Expected a member name after '.'."));
                            return KesType.Unknown;
                        }

                        value = KesType.Unknown;
                        continue;
                    }

                    if (Match(TokenKind.OpenBracket))
                    {
                        var bracket = Previous;
                        var indexType = ParseExpression();
                        Consume(TokenKind.CloseBracket);
                        checker.RequireAssignable(KesType.Number, indexType, Location(bracket), "Array index must be number.");
                        if (value.Kind == KesTypeKind.Array)
                        {
                            value = value.ElementType!;
                        }
                        else if (value.Kind != KesTypeKind.Unknown)
                        {
                            checker.diagnostics.Add(Diagnostic(checker.document.Document.ProjectRelativePath, Location(bracket), $"Cannot index non-array type {value}."));
                            value = KesType.Unknown;
                        }

                        continue;
                    }

                    break;
                }

                return value;
            }

            private KesType ParsePrimary()
            {
                if (IsAtEnd())
                {
                    return KesType.Unknown;
                }

                var token = Advance();
                return token.Kind switch
                {
                    TokenKind.NumberLiteral => KesType.Number,
                    TokenKind.StringLiteral => KesType.String,
                    TokenKind.Keyword when token.Lexeme is "true" or "false" => KesType.Bool,
                    TokenKind.Keyword when token.Lexeme == "null" => KesType.Null,
                    TokenKind.OpenParen => ParseParenthesizedOrCall(token),
                    TokenKind.OpenBracket => ParseArrayLiteral(token),
                    TokenKind.Identifier or TokenKind.Keyword => ParseIdentifierLike(token),
                    _ => KesType.Unknown,
                };
            }

            private KesType ParseParenthesizedOrCall(Token open)
            {
                var start = position;
                var depth = 1;
                while (position < tokens.Count && depth > 0)
                {
                    if (tokens[position].Kind == TokenKind.OpenParen)
                    {
                        depth++;
                    }
                    else if (tokens[position].Kind == TokenKind.CloseParen)
                    {
                        depth--;
                    }

                    position++;
                }

                var inner = tokens.Skip(start).Take(Math.Max(0, position - start - 1)).ToArray();
                return checker.Evaluate(inner, requireValue);
            }

            private KesType ParseArrayLiteral(Token open)
            {
                var elements = new List<KesType>();
                if (Match(TokenKind.CloseBracket))
                {
                    return KesType.Array(KesType.Unknown);
                }

                while (!IsAtEnd())
                {
                    elements.Add(ParseExpression());
                    if (Match(TokenKind.Comma))
                    {
                        continue;
                    }

                    Consume(TokenKind.CloseBracket);
                    break;
                }

                var elementType = elements.FirstOrDefault(type => type.Kind != KesTypeKind.Unknown) ?? KesType.Unknown;
                foreach (var type in elements)
                {
                    if (!elementType.IsAssignableFrom(type))
                    {
                        checker.diagnostics.Add(Diagnostic(checker.document.Document.ProjectRelativePath, Location(open), $"Array elements must have a common type, but found {elementType} and {type}."));
                        return KesType.Array(KesType.Unknown);
                    }
                }

                return KesType.Array(elementType);
            }

            private KesType ParseIdentifierLike(Token token)
            {
                if (Match(TokenKind.OpenParen))
                {
                    Consume(TokenKind.CloseParen);
                    return checker.CheckCall(token.Lexeme, [], Location(token), requireValue);
                }

                if (!IsAtEnd() && !IsBinaryOrDelimiter(Current.Kind) && checker.ResolveCallable(token.Lexeme) is not null)
                {
                    var argStart = position;
                    while (!IsAtEnd() && !IsBinaryOrDelimiter(Current.Kind))
                    {
                        position++;
                    }

                    var args = tokens.Skip(argStart).Take(position - argStart).ToArray();
                    return checker.CheckCall(token.Lexeme, args, Location(token), requireValue);
                }

                return checker.ResolveValue(token.Lexeme);
            }

            private bool Match(TokenKind kind)
            {
                if (IsAtEnd() || Current.Kind != kind)
                {
                    return false;
                }

                position++;
                return true;
            }

            private Token? AdvanceIf(TokenKind kind)
            {
                if (IsAtEnd() || Current.Kind != kind)
                {
                    return null;
                }

                return Advance();
            }

            private void Consume(TokenKind kind)
            {
                if (!Match(kind))
                {
                    return;
                }
            }

            private Token Advance()
            {
                return tokens[position++];
            }

            private bool IsAtEnd()
            {
                return position >= tokens.Count;
            }

            private Token Current => tokens[position];

            private Token Previous => tokens[position - 1];

            private static SourceLocation Location(Token token)
            {
                return new SourceLocation(token.Line, token.Column);
            }

            private static bool IsBinaryOrDelimiter(TokenKind kind)
            {
                return kind is TokenKind.Plus or TokenKind.Minus or TokenKind.Star or TokenKind.Slash
                    or TokenKind.DoubleEquals or TokenKind.NotEquals
                    or TokenKind.Less or TokenKind.LessOrEqual or TokenKind.Greater or TokenKind.GreaterOrEqual
                    or TokenKind.AndAnd or TokenKind.OrOr
                    or TokenKind.Comma or TokenKind.CloseParen or TokenKind.CloseBracket;
            }
        }
    }

    private sealed record DocumentTypeContext(
        ScriptDocument Document,
        IReadOnlyDictionary<string, KesType> ModuleVariables,
        IReadOnlyDictionary<string, CallableSignature> Callables);

    private sealed record TypeCheckingContext(
        IReadOnlyDictionary<string, DocumentTypeContext> DocumentsByModule,
        BuiltInSignatureRegistry BuiltIns)
    {
        public static TypeCheckingContext From(IReadOnlyList<DefinitionCollectionResult> collections, BuiltInSignatureRegistry builtIns)
        {
            var documents = collections
                .GroupBy(static collection => collection.Document.ModuleName, StringComparer.Ordinal)
                .ToDictionary(
                    static group => group.Key,
                    group => BuildDocumentContext(group.First().Document),
                    StringComparer.Ordinal);
            return new TypeCheckingContext(documents, builtIns);
        }

        private static DocumentTypeContext BuildDocumentContext(ScriptDocument document)
        {
            var variables = new Dictionary<string, KesType>(StringComparer.Ordinal);
            var callables = new Dictionary<string, CallableSignature>(StringComparer.Ordinal);

            foreach (var statement in document.Syntax.Statements)
            {
                switch (statement)
                {
                    case VarStatementSyntax varStatement:
                        variables[varStatement.Name] = varStatement.TypeTokens.Count > 0
                            ? ParseType(varStatement.TypeTokens)
                            : KesType.Unknown;
                        break;

                    case StandbyStatementSyntax standby:
                        foreach (var entry in standby.Entries)
                        {
                            variables[entry.InstanceName] = KesType.Actor;
                        }

                        break;

                    case FunctionDeclarationSyntax function:
                        callables[function.Name] = BuildSignature(function);
                        break;
                }
            }

            return new DocumentTypeContext(document, variables, callables);
        }

        private static CallableSignature BuildSignature(FunctionDeclarationSyntax function)
        {
            var parameters = function.Parameters
                .Select(static parameter => new CallableParameter(parameter.Name, ParseType(parameter.TypeTokens)))
                .ToArray();
            var returnType = function.ReturnTypeTokens.Count == 0 ? KesType.Void : ParseType(function.ReturnTypeTokens);
            return new CallableSignature(function.Name, parameters, returnType);
        }
    }
}
