using System.Globalization;
using System.Text;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Lexing;
using KoromoEventScript.Cli.Parsing;
using KoromoEventScript.Cli.ProjectSystem;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Compilation;

public sealed class KlibCompiler
{
    private readonly BuiltInSignatureRegistry builtIns = new();

    public KlibCompilationResult Compile(
        ProjectConfig config,
        SemanticAnalysisResult semanticResult,
        ScriptDocument document,
        bool embedLocalizedText = false)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(semanticResult);
        ArgumentNullException.ThrowIfNull(document);

        var context = new CompilationContext(config, semanticResult, document, builtIns, embedLocalizedText);
        context.Compile();
        return context.Diagnostics.Count > 0
            ? KlibCompilationResult.Failure(context.Diagnostics)
            : KlibCompilationResult.Success(context.BuildDocument());
    }

    private sealed class CompilationContext
    {
        private const string ScenarioSay = "scenario.say";
        private const string ScenarioNar = "scenario.nar";
        private readonly ProjectConfig config;
        private readonly SemanticAnalysisResult semanticResult;
        private readonly ScriptDocument document;
        private readonly BuiltInSignatureRegistry builtIns;
        private readonly ConstantPoolBuilder constantPool = new();
        private readonly List<InstructionBuilder> instructions = [];
        private readonly List<VariableSlot> variables = [];
        private readonly List<Diagnostic> diagnostics = [];
        private readonly List<DefinitionScope> scopes;
        private readonly HashSet<string> actorNames;
        private readonly Dictionary<string, int> labelInstructionIndexes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> currentLocals = new(StringComparer.Ordinal);
        private readonly Stack<Dictionary<string, int>> localScopes = new();
        private readonly Stack<LoopLabels> loops = new();
        private readonly bool embedLocalizedText;
        private int nextScopeId;
        private int nextHiddenVariableId;
        private int nextSyntheticLabelId;

        public CompilationContext(
            ProjectConfig config,
            SemanticAnalysisResult semanticResult,
            ScriptDocument document,
            BuiltInSignatureRegistry builtIns,
            bool embedLocalizedText)
        {
            this.config = config;
            this.semanticResult = semanticResult;
            this.document = document;
            this.builtIns = builtIns;
            this.embedLocalizedText = embedLocalizedText;
            scopes = semanticResult.DefinitionCollections
                .FirstOrDefault(result => string.Equals(result.Document.ProjectRelativePath, document.ProjectRelativePath, StringComparison.Ordinal))?
                .DefinitionTable.Scopes
                .ToList() ?? [];
            actorNames = semanticResult.DefinitionCollections
                .SelectMany(static result => result.DefinitionTable.Definitions)
                .Where(static definition => definition.Kind == DefinitionKind.Actor)
                .Select(static definition => definition.Name)
                .ToHashSet(StringComparer.Ordinal);
        }

        public IReadOnlyList<Diagnostic> Diagnostics => diagnostics;

        public void Compile()
        {
            constantPool.GetStringIndex(document.ModuleName);
            constantPool.GetStringIndex(document.ProjectRelativePath);
            constantPool.GetStringIndex(Path.GetFileName(document.ProjectRelativePath));
            constantPool.GetStringIndex(ScenarioSay);
            constantPool.GetStringIndex(ScenarioNar);

            localScopes.Push(currentLocals);

            foreach (var statement in document.Syntax.Statements)
            {
                CompileStatement(statement);
            }

            instructions.Add(new InstructionBuilder(KlibOpCode.End, null, KlibMappingKind.Synthetic));

            ResolveLabelsAndOffsets();
        }

        public KlibDocument BuildDocument()
        {
            var constants = constantPool.Build();
            var finalizedInstructions = instructions
                .Select((instruction, index) => instruction.ToInstruction(index))
                .ToArray();
            var labels = labelInstructionIndexes
                .OrderBy(static entry => entry.Value)
                .Select(entry =>
                {
                    var offset = finalizedInstructions[entry.Value].Offset;
                    var nameIndex = constantPool.GetStringIndex(entry.Key);
                    var flags = entry.Key.StartsWith("#", StringComparison.Ordinal) ? 1 : 0;
                    return new KlibLabel(nameIndex, offset, flags);
                })
                .ToArray();
            var debug = new KlibDebugInfo(
                constantPool.GetStringIndex(document.ModuleName),
                constantPool.GetStringIndex(Path.GetFileName(document.ProjectRelativePath)),
                finalizedInstructions
                    .Where(static instruction => instruction.Source is not null)
                    .Select(instruction => new KlibSourceMapping(
                        instruction.Offset,
                        constantPool.GetStringIndex(document.ProjectRelativePath),
                        instruction.Source!.Value.Line,
                        instruction.Source.Value.Column,
                        0,
                        0,
                        instruction.MappingKind))
                    .ToArray());

            return new KlibDocument(
                new KlibVersion(1, 0, 0),
                new KlibModuleInfo(
                    BuildScriptId(document.ProjectRelativePath),
                    $"module.{NormalizeIdentifier(document.ModuleName)}",
                    document.ProjectRelativePath,
                    labelInstructionIndexes.Keys.FirstOrDefault()),
                BuildImports(),
                constants,
                variables.Select(variable => variable.ToKlibVariable()).ToArray(),
                finalizedInstructions,
                labels,
                debug);
        }

        private IReadOnlyList<KlibImport> BuildImports()
        {
            if (semanticResult.ImportGraph is null ||
                !semanticResult.ImportGraph.DirectImports.TryGetValue(document.ModuleName, out var directImports))
            {
                return [];
            }

            return directImports
                .Select(importName => semanticResult.ImportGraph.OrderedDocuments.FirstOrDefault(
                    candidate => string.Equals(candidate.ModuleName, importName, StringComparison.Ordinal)))
                .Where(static candidate => candidate is not null)
                .Select(candidate => new KlibImport(
                    $"module.{NormalizeIdentifier(candidate!.ModuleName)}",
                    BuildScriptId(candidate.ProjectRelativePath),
                    candidate.ProjectRelativePath,
                    null))
                .ToArray();
        }

        private void CompileStatement(StatementSyntax statement)
        {
            switch (statement)
            {
                case ImportStatementSyntax:
                case ActorDeclarationSyntax:
                case EnumDeclarationSyntax:
                case FunctionDeclarationSyntax:
                case ClassDeclarationSyntax:
                    return;

                case VarStatementSyntax varStatement:
                    CompileVar(varStatement);
                    return;

                case AssignmentStatementSyntax assignment:
                    CompileAssignment(assignment);
                    return;

                case LabelStatementSyntax label:
                    EmitLabel(label.Tag, label.TagLocation, publicLabel: true);
                    return;

                case JumpStatementSyntax jump:
                    instructions.Add(InstructionBuilder.Jump(jump.Tag, jump.TagLocation));
                    return;

                case CommandStatementSyntax command:
                    CompileCommand(command.Name, command.Arguments, command.NameLocation, requiresValue: false);
                    return;

                case LessStatementSyntax less:
                    CompileLess(less);
                    return;

                case SayStatementSyntax say:
                    CompileSay(say);
                    return;

                case NarStatementSyntax nar:
                    CompileNar(nar);
                    return;

                case SelectStatementSyntax select:
                    CompileSelect(select);
                    return;

                case IfStatementSyntax ifStatement:
                    CompileIf(ifStatement);
                    return;

                case WhileStatementSyntax whileStatement:
                    CompileWhile(whileStatement);
                    return;

                case ForStatementSyntax forStatement:
                    CompileFor(forStatement);
                    return;

                default:
                    diagnostics.Add(Diagnostic("KES2016", GetStatementLocation(statement), $"Unsupported statement '{statement.GetType().Name}' for .klib compilation."));
                    return;
            }
        }

        private void CompileVar(VarStatementSyntax varStatement)
        {
            var variableType = InferVariableType(varStatement.TypeTokens, varStatement.ValueTokens);
            var slot = DeclareVariable(varStatement.Name, variableType, KlibScopeKind.Script, GetScopeId(), varStatement.NameLocation);
            if (varStatement.ValueTokens.Count > 0)
            {
                CompileExpression(varStatement.ValueTokens, requireValue: true);
            }
            else
            {
                instructions.Add(new InstructionBuilder(KlibOpCode.PushNull, varStatement.NameLocation, KlibMappingKind.Expression));
            }

            instructions.Add(new InstructionBuilder(KlibOpCode.DefVar, varStatement.NameLocation, KlibMappingKind.Statement, slot.Index));
        }

        private void CompileAssignment(AssignmentStatementSyntax assignment)
        {
            if (!TryResolveVariable(assignment.TargetName, out var targetIndex))
            {
                diagnostics.Add(Diagnostic("KES2017", assignment.TargetLocation, $"Unknown assignment target '{assignment.TargetName}' during .klib compilation."));
                return;
            }

            CompileExpression(assignment.ValueTokens, requireValue: true);
            instructions.Add(new InstructionBuilder(KlibOpCode.StoreVar, assignment.TargetLocation, KlibMappingKind.Statement, targetIndex));
        }

        private void CompileLess(LessStatementSyntax less)
        {
            foreach (var item in less.Items)
            {
                switch (item)
                {
                    case LessCommandItemSyntax command:
                        CompileCommand(less.Name, less.SharedArguments.Concat(command.Arguments).ToArray(), less.NameLocation, requiresValue: false);
                        break;

                    case LessNestedStatementSyntax nested:
                        CompileLess(nested.Statement);
                        break;
                }
            }
        }

        private void CompileSay(SayStatementSyntax say)
        {
            if (!string.IsNullOrWhiteSpace(say.Tag))
            {
                EmitLabel(say.Tag!, say.TagLocation ?? say.SpeakerLocation, publicLabel: true);
            }

            foreach (var (line, index) in say.Lines.Select((line, index) => (line, index)))
            {
                EmitActorReference(say.Speaker, say.SpeakerLocation);
                EmitTextValue(line, say.Tag, index, say.SpeakerLocation);
                instructions.Add(new InstructionBuilder(
                    KlibOpCode.SysCallVoid,
                    say.SpeakerLocation,
                    KlibMappingKind.TextBody,
                    constantPool.GetStringIndex(ScenarioSay),
                    2));
            }
        }

        private void CompileNar(NarStatementSyntax nar)
        {
            if (!string.IsNullOrWhiteSpace(nar.Tag))
            {
                EmitLabel(nar.Tag!, nar.TagLocation ?? new SourceLocation(1, 1), publicLabel: true);
            }

            foreach (var (line, index) in nar.Lines.Select((line, index) => (line, index)))
            {
                EmitTextValue(line, nar.Tag, index, nar.TagLocation ?? new SourceLocation(1, 1));
                instructions.Add(new InstructionBuilder(
                    KlibOpCode.SysCallVoid,
                    nar.TagLocation ?? new SourceLocation(1, 1),
                    KlibMappingKind.TextBody,
                    constantPool.GetStringIndex(ScenarioNar),
                    1));
            }
        }

        private void CompileSelect(SelectStatementSyntax select)
        {
            instructions.Add(new InstructionBuilder(KlibOpCode.PushNull, GetStatementLocation(select), KlibMappingKind.Statement));
            var cases = select.Cases
                .Select(caseClause => new SelectCaseBuilder(constantPool.GetStringIndex(caseClause.Text), caseClause.Tag))
                .ToArray();
            instructions.Add(InstructionBuilder.Select(cases, GetStatementLocation(select)));
        }

        private void CompileIf(IfStatementSyntax ifStatement)
        {
            var endLabel = CreateSyntheticLabel("if_end");
            var nextLabel = CreateSyntheticLabel("if_next");
            CompileExpression(ifStatement.ConditionTokens, requireValue: true);
            instructions.Add(InstructionBuilder.JumpFalse(nextLabel, ifStatement.IfLocation));
            PushBlockScope();
            foreach (var statement in ifStatement.Body.Statements)
            {
                CompileStatement(statement);
            }
            PopBlockScope();
            instructions.Add(InstructionBuilder.Jump(endLabel, ifStatement.IfLocation));
            EmitLabel(nextLabel, ifStatement.IfLocation, publicLabel: false);

            foreach (var elseIfClause in ifStatement.ElseIfClauses)
            {
                var elseIfNext = CreateSyntheticLabel("elseif_next");
                CompileExpression(elseIfClause.ConditionTokens, requireValue: true);
                instructions.Add(InstructionBuilder.JumpFalse(elseIfNext, elseIfClause.ElseIfLocation));
                PushBlockScope();
                foreach (var statement in elseIfClause.Body.Statements)
                {
                    CompileStatement(statement);
                }
                PopBlockScope();
                instructions.Add(InstructionBuilder.Jump(endLabel, elseIfClause.ElseIfLocation));
                EmitLabel(elseIfNext, elseIfClause.ElseIfLocation, publicLabel: false);
            }

            if (ifStatement.ElseBody is not null)
            {
                PushBlockScope();
                foreach (var statement in ifStatement.ElseBody.Statements)
                {
                    CompileStatement(statement);
                }
                PopBlockScope();
            }

            EmitLabel(endLabel, ifStatement.IfLocation, publicLabel: false);
        }

        private void CompileWhile(WhileStatementSyntax whileStatement)
        {
            var startLabel = CreateSyntheticLabel("while_start");
            var endLabel = CreateSyntheticLabel("while_end");
            EmitLabel(startLabel, whileStatement.WhileLocation, publicLabel: false);
            CompileExpression(whileStatement.ConditionTokens, requireValue: true);
            instructions.Add(InstructionBuilder.JumpFalse(endLabel, whileStatement.WhileLocation));
            loops.Push(new LoopLabels(startLabel, endLabel));
            PushBlockScope();
            foreach (var statement in whileStatement.Body.Statements)
            {
                CompileStatement(statement);
            }
            PopBlockScope();
            loops.Pop();
            instructions.Add(InstructionBuilder.Jump(startLabel, whileStatement.WhileLocation));
            EmitLabel(endLabel, whileStatement.WhileLocation, publicLabel: false);
        }

        private void CompileFor(ForStatementSyntax forStatement)
        {
            PushBlockScope();
            var iterableSlot = DeclareHiddenVariable("for_iter", KlibVariableType.Array, KlibScopeKind.Block, GetScopeId());
            var indexSlot = DeclareHiddenVariable("for_index", KlibVariableType.Number, KlibScopeKind.Block, GetScopeId());
            var elementSlot = DeclareVariable(forStatement.VariableName, KlibVariableType.Unknown, KlibScopeKind.Block, GetScopeId(), forStatement.VariableLocation);

            CompileExpression(forStatement.IterableTokens, requireValue: true);
            instructions.Add(new InstructionBuilder(KlibOpCode.DefVar, forStatement.ForLocation, KlibMappingKind.Statement, iterableSlot.Index));
            instructions.Add(new InstructionBuilder(KlibOpCode.PushInt, forStatement.ForLocation, KlibMappingKind.Expression, 0));
            instructions.Add(new InstructionBuilder(KlibOpCode.DefVar, forStatement.ForLocation, KlibMappingKind.Statement, indexSlot.Index));

            var startLabel = CreateSyntheticLabel("for_start");
            var endLabel = CreateSyntheticLabel("for_end");
            EmitLabel(startLabel, forStatement.ForLocation, publicLabel: false);
            instructions.Add(new InstructionBuilder(KlibOpCode.LoadVar, forStatement.ForLocation, KlibMappingKind.Expression, indexSlot.Index));
            instructions.Add(new InstructionBuilder(KlibOpCode.LoadVar, forStatement.ForLocation, KlibMappingKind.Expression, iterableSlot.Index));
            instructions.Add(new InstructionBuilder(
                KlibOpCode.Call,
                forStatement.ForLocation,
                KlibMappingKind.Expression,
                constantPool.GetStringIndex("array_len"),
                1));
            instructions.Add(new InstructionBuilder(KlibOpCode.Lt, forStatement.ForLocation, KlibMappingKind.Expression));
            instructions.Add(InstructionBuilder.JumpFalse(endLabel, forStatement.ForLocation));

            instructions.Add(new InstructionBuilder(KlibOpCode.LoadVar, forStatement.ForLocation, KlibMappingKind.Expression, iterableSlot.Index));
            instructions.Add(new InstructionBuilder(KlibOpCode.LoadVar, forStatement.ForLocation, KlibMappingKind.Expression, indexSlot.Index));
            instructions.Add(new InstructionBuilder(KlibOpCode.ArrayGet, forStatement.ForLocation, KlibMappingKind.Expression));
            instructions.Add(new InstructionBuilder(KlibOpCode.StoreVar, forStatement.ForLocation, KlibMappingKind.Statement, elementSlot.Index));

            loops.Push(new LoopLabels(startLabel, endLabel));
            foreach (var statement in forStatement.Body.Statements)
            {
                CompileStatement(statement);
            }
            loops.Pop();

            instructions.Add(new InstructionBuilder(KlibOpCode.LoadVar, forStatement.ForLocation, KlibMappingKind.Expression, indexSlot.Index));
            instructions.Add(new InstructionBuilder(KlibOpCode.PushInt, forStatement.ForLocation, KlibMappingKind.Expression, 1));
            instructions.Add(new InstructionBuilder(KlibOpCode.Add, forStatement.ForLocation, KlibMappingKind.Expression));
            instructions.Add(new InstructionBuilder(KlibOpCode.StoreVar, forStatement.ForLocation, KlibMappingKind.Statement, indexSlot.Index));
            instructions.Add(InstructionBuilder.Jump(startLabel, forStatement.ForLocation));
            EmitLabel(endLabel, forStatement.ForLocation, publicLabel: false);
            PopBlockScope();
        }

        private void CompileCommand(string name, IReadOnlyList<Token> tokens, SourceLocation location, bool requiresValue)
        {
            var arguments = SplitArguments(tokens);
            foreach (var argument in arguments)
            {
                CompileExpression(argument, requireValue: true);
            }

            var opCode = requiresValue ? KlibOpCode.Call : KlibOpCode.CallVoid;
            instructions.Add(new InstructionBuilder(
                opCode,
                location,
                KlibMappingKind.Statement,
                constantPool.GetStringIndex(name),
                arguments.Count));
        }

        private void CompileExpression(IReadOnlyList<Token> tokens, bool requireValue)
        {
            if (tokens.Count == 0)
            {
                instructions.Add(new InstructionBuilder(KlibOpCode.PushNull, new SourceLocation(1, 1), KlibMappingKind.Expression));
                return;
            }

            var parser = new ExpressionCompiler(this, tokens, requireValue);
            parser.ParseExpression();
        }

        private void EmitActorReference(string actorName, SourceLocation location)
        {
            if (TryResolveVariable(actorName, out var variableIndex))
            {
                instructions.Add(new InstructionBuilder(KlibOpCode.LoadVar, location, KlibMappingKind.Expression, variableIndex));
                return;
            }

            var actorId = constantPool.GetActorReferenceIndex($"actor.{NormalizeIdentifier(actorName)}");
            instructions.Add(new InstructionBuilder(KlibOpCode.PushConst, location, KlibMappingKind.Expression, actorId));
        }

        private void EmitTextValue(TextLineSyntax line, string? tag, int index, SourceLocation location)
        {
            if (!line.IsExpressionLine)
            {
                var constantIndex = string.IsNullOrWhiteSpace(tag) || embedLocalizedText
                    ? constantPool.GetStringConstantIndex(line.Text)
                    : constantPool.GetLocaleKeyIndex(BuildLocaleKey(tag!, index));
                instructions.Add(new InstructionBuilder(KlibOpCode.PushConst, location, KlibMappingKind.TextBody, constantIndex));
                return;
            }

            var expressionText = line.Text.TrimStart('@').Trim();
            try
            {
                var tokens = KeLexer.Lex(expressionText).Tokens
                    .Where(static token => token.Kind is not (TokenKind.Newline or TokenKind.EndOfFile))
                    .ToArray();
                CompileExpression(tokens, requireValue: true);
            }
            catch (LexerException)
            {
                var constantIndex = constantPool.GetStringConstantIndex(expressionText);
                instructions.Add(new InstructionBuilder(KlibOpCode.PushConst, location, KlibMappingKind.TextBody, constantIndex));
            }
        }

        private void ResolveLabelsAndOffsets()
        {
            var offset = 0;
            foreach (var instruction in instructions)
            {
                instruction.Offset = offset;
                offset += instruction.GetSize();
            }

            foreach (var instruction in instructions)
            {
                if (instruction.OpCode == KlibOpCode.Label)
                {
                    var labelName = constantPool.GetStringValue(instruction.Operands[0]);
                    labelInstructionIndexes[labelName] = instruction.IndexHint;
                }
            }

            foreach (var instruction in instructions)
            {
                instruction.ResolveOffsets(instructions, labelInstructionIndexes, constantPool, diagnostics);
            }
        }

        private void EmitLabel(string name, SourceLocation location, bool publicLabel)
        {
            var nameIndex = constantPool.GetStringIndex(name);
            var builder = new InstructionBuilder(KlibOpCode.Label, location, KlibMappingKind.Statement, nameIndex, publicLabel ? 1 : 0)
            {
                IndexHint = instructions.Count,
            };
            instructions.Add(builder);
        }

        private VariableSlot DeclareVariable(string name, KlibVariableType type, KlibScopeKind scopeKind, int scopeId, SourceLocation location)
        {
            var stableId = constantPool.GetStringIndex($"v.{NormalizeIdentifier(document.ModuleName)}.{NormalizeIdentifier(name)}.{variables.Count}");
            var nameIndex = constantPool.GetStringIndex(name);
            var slot = new VariableSlot(variables.Count, name, stableId, nameIndex, type, scopeKind, scopeId, null);
            variables.Add(slot);
            localScopes.Peek()[name] = slot.Index;
            return slot;
        }

        private VariableSlot DeclareHiddenVariable(string prefix, KlibVariableType type, KlibScopeKind scopeKind, int scopeId)
        {
            var name = $"${prefix}_{nextHiddenVariableId++}";
            return DeclareVariable(name, type, scopeKind, scopeId, new SourceLocation(1, 1));
        }

        private bool TryResolveVariable(string name, out int variableIndex)
        {
            foreach (var scope in localScopes.Reverse())
            {
                if (scope.TryGetValue(name, out variableIndex))
                {
                    return true;
                }
            }

            variableIndex = -1;
            return false;
        }

        private void PushBlockScope()
        {
            localScopes.Push(new Dictionary<string, int>(StringComparer.Ordinal));
        }

        private void PopBlockScope()
        {
            localScopes.Pop();
        }

        private int GetScopeId()
        {
            return nextScopeId++;
        }

        private string CreateSyntheticLabel(string prefix)
        {
            return $"__{prefix}_{nextSyntheticLabelId++}";
        }

        private static string NormalizeIdentifier(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '_');
            }

            return builder.ToString();
        }

        private static string BuildScriptId(string relativePath)
        {
            return Path.ChangeExtension(relativePath.Replace('\\', '/'), null) ?? relativePath.Replace('\\', '/');
        }

        private static string BuildLocaleKey(string tag, int index)
        {
            return $"{tag.TrimStart('#')}_{index + 1}";
        }

        private KlibVariableType InferVariableType(IReadOnlyList<Token> typeTokens, IReadOnlyList<Token> valueTokens)
        {
            if (typeTokens.Count > 0)
            {
                var typeName = string.Concat(typeTokens.Select(static token => token.Lexeme));
                return typeName switch
                {
                    "number" => KlibVariableType.Number,
                    "bool" => KlibVariableType.Bool,
                    "string" => KlibVariableType.String,
                    "Actor" => KlibVariableType.Actor,
                    "number[]" => KlibVariableType.Array,
                    "bool[]" => KlibVariableType.Array,
                    "string[]" => KlibVariableType.Array,
                    "Actor[]" => KlibVariableType.Array,
                    _ => KlibVariableType.Unknown,
                };
            }

            if (valueTokens.Count == 0)
            {
                return KlibVariableType.Unknown;
            }

            return valueTokens[0].Kind switch
            {
                TokenKind.NumberLiteral => KlibVariableType.Number,
                TokenKind.StringLiteral => KlibVariableType.String,
                TokenKind.OpenBracket => KlibVariableType.Array,
                TokenKind.Keyword when valueTokens[0].Lexeme is "true" or "false" => KlibVariableType.Bool,
                TokenKind.Identifier when actorNames.Contains(valueTokens[0].Lexeme) => KlibVariableType.Actor,
                _ => KlibVariableType.Unknown,
            };
        }

        private Diagnostic Diagnostic(string code, SourceLocation location, string message)
        {
            return new Diagnostic(DiagnosticLevel.Error, code, document.ProjectRelativePath, location.Line, location.Column, message);
        }

        private static SourceLocation GetStatementLocation(StatementSyntax statement)
        {
            return statement switch
            {
                VarStatementSyntax varStatement => varStatement.NameLocation,
                AssignmentStatementSyntax assignment => assignment.TargetLocation,
                FunctionDeclarationSyntax function => function.NameLocation,
                ActorDeclarationSyntax actor => actor.NameLocation,
                EnumDeclarationSyntax @enum => @enum.NameLocation,
                ClassDeclarationSyntax @class => @class.NameLocation,
                LabelStatementSyntax label => label.TagLocation,
                JumpStatementSyntax jump => jump.TagLocation,
                CommandStatementSyntax command => command.NameLocation,
                LessStatementSyntax less => less.NameLocation,
                SayStatementSyntax say => say.SpeakerLocation,
                NarStatementSyntax nar => nar.TagLocation ?? new SourceLocation(1, 1),
                IfStatementSyntax ifStatement => ifStatement.IfLocation,
                WhileStatementSyntax whileStatement => whileStatement.WhileLocation,
                ForStatementSyntax forStatement => forStatement.ForLocation,
                _ => new SourceLocation(1, 1),
            };
        }

        private sealed record LoopLabels(string ContinueLabel, string BreakLabel);

        private sealed record VariableSlot(
            int Index,
            string Name,
            int StableIdIndex,
            int NameIndex,
            KlibVariableType Type,
            KlibScopeKind ScopeKind,
            int ScopeId,
            int? InitialValueIndex)
        {
            public KlibVariable ToKlibVariable()
            {
                return new KlibVariable(StableIdIndex, NameIndex, Type, ScopeKind, ScopeId, InitialValueIndex);
            }
        }

        private sealed class ExpressionCompiler
        {
            private readonly CompilationContext context;
            private readonly IReadOnlyList<Token> tokens;
            private readonly bool requireValue;
            private int position;

            public ExpressionCompiler(CompilationContext context, IReadOnlyList<Token> tokens, bool requireValue)
            {
                this.context = context;
                this.tokens = tokens;
                this.requireValue = requireValue;
            }

            public void ParseExpression()
            {
                ParseLogicalOr();
            }

            private void ParseLogicalOr()
            {
                ParseLogicalAnd();
                while (Match(TokenKind.OrOr))
                {
                    var token = Previous;
                    ParseLogicalAnd();
                    context.instructions.Add(new InstructionBuilder(KlibOpCode.Or, ToLocation(token), KlibMappingKind.Expression));
                }
            }

            private void ParseLogicalAnd()
            {
                ParseEquality();
                while (Match(TokenKind.AndAnd))
                {
                    var token = Previous;
                    ParseEquality();
                    context.instructions.Add(new InstructionBuilder(KlibOpCode.And, ToLocation(token), KlibMappingKind.Expression));
                }
            }

            private void ParseEquality()
            {
                ParseComparison();
                while (Match(TokenKind.DoubleEquals) || Match(TokenKind.NotEquals))
                {
                    var op = Previous.Kind == TokenKind.DoubleEquals ? KlibOpCode.Eq : KlibOpCode.Neq;
                    var token = Previous;
                    ParseComparison();
                    context.instructions.Add(new InstructionBuilder(op, ToLocation(token), KlibMappingKind.Expression));
                }
            }

            private void ParseComparison()
            {
                ParseTerm();
                while (Match(TokenKind.Less) || Match(TokenKind.LessOrEqual) || Match(TokenKind.Greater) || Match(TokenKind.GreaterOrEqual))
                {
                    var token = Previous;
                    ParseTerm();
                    var op = token.Kind switch
                    {
                        TokenKind.Less => KlibOpCode.Lt,
                        TokenKind.LessOrEqual => KlibOpCode.Le,
                        TokenKind.Greater => KlibOpCode.Gt,
                        _ => KlibOpCode.Ge,
                    };
                    context.instructions.Add(new InstructionBuilder(op, ToLocation(token), KlibMappingKind.Expression));
                }
            }

            private void ParseTerm()
            {
                ParseFactor();
                while (Match(TokenKind.Plus) || Match(TokenKind.Minus))
                {
                    var token = Previous;
                    ParseFactor();
                    context.instructions.Add(new InstructionBuilder(
                        token.Kind == TokenKind.Plus ? KlibOpCode.Add : KlibOpCode.Sub,
                        ToLocation(token),
                        KlibMappingKind.Expression));
                }
            }

            private void ParseFactor()
            {
                ParseUnary();
                while (Match(TokenKind.Star) || Match(TokenKind.Slash))
                {
                    var token = Previous;
                    ParseUnary();
                    context.instructions.Add(new InstructionBuilder(
                        token.Kind == TokenKind.Star ? KlibOpCode.Mul : KlibOpCode.Div,
                        ToLocation(token),
                        KlibMappingKind.Expression));
                }
            }

            private void ParseUnary()
            {
                if (Match(TokenKind.Bang))
                {
                    var token = Previous;
                    ParseUnary();
                    context.instructions.Add(new InstructionBuilder(KlibOpCode.Not, ToLocation(token), KlibMappingKind.Expression));
                    return;
                }

                if (Match(TokenKind.Minus))
                {
                    var token = Previous;
                    ParseUnary();
                    context.instructions.Add(new InstructionBuilder(KlibOpCode.Neg, ToLocation(token), KlibMappingKind.Expression));
                    return;
                }

                if (Match(TokenKind.Plus))
                {
                    ParseUnary();
                    return;
                }

                ParsePostfix();
            }

            private void ParsePostfix()
            {
                ParsePrimary();
                while (Match(TokenKind.OpenBracket))
                {
                    var token = Previous;
                    ParseExpression();
                    Consume(TokenKind.CloseBracket);
                    context.instructions.Add(new InstructionBuilder(KlibOpCode.ArrayGet, ToLocation(token), KlibMappingKind.Expression));
                }
            }

            private void ParsePrimary()
            {
                if (IsAtEnd())
                {
                    context.instructions.Add(new InstructionBuilder(KlibOpCode.PushNull, new SourceLocation(1, 1), KlibMappingKind.Expression));
                    return;
                }

                var token = Advance();
                switch (token.Kind)
                {
                    case TokenKind.NumberLiteral:
                        EmitNumber(token);
                        return;

                    case TokenKind.StringLiteral:
                        context.instructions.Add(new InstructionBuilder(
                            KlibOpCode.PushConst,
                            ToLocation(token),
                            KlibMappingKind.Expression,
                            context.constantPool.GetStringConstantIndex(token.Lexeme)));
                        return;

                    case TokenKind.Keyword when token.Lexeme == "true":
                        context.instructions.Add(new InstructionBuilder(KlibOpCode.PushTrue, ToLocation(token), KlibMappingKind.Expression));
                        return;

                    case TokenKind.Keyword when token.Lexeme == "false":
                        context.instructions.Add(new InstructionBuilder(KlibOpCode.PushFalse, ToLocation(token), KlibMappingKind.Expression));
                        return;

                    case TokenKind.Keyword when token.Lexeme == "null":
                        context.instructions.Add(new InstructionBuilder(KlibOpCode.PushNull, ToLocation(token), KlibMappingKind.Expression));
                        return;

                    case TokenKind.OpenParen:
                        ParseExpression();
                        Consume(TokenKind.CloseParen);
                        return;

                    case TokenKind.OpenBracket:
                        ParseArrayLiteral(token);
                        return;

                    case TokenKind.Identifier or TokenKind.Keyword:
                        ParseIdentifierLike(token);
                        return;

                    default:
                        context.diagnostics.Add(context.Diagnostic("KES2016", ToLocation(token), $"Unsupported expression token '{token.Lexeme}' for .klib compilation."));
                        context.instructions.Add(new InstructionBuilder(KlibOpCode.PushNull, ToLocation(token), KlibMappingKind.Expression));
                        return;
                }
            }

            private void ParseIdentifierLike(Token token)
            {
                if (Match(TokenKind.OpenParen))
                {
                    var arguments = ReadArgumentsUntilCloseParen();
                    foreach (var argument in arguments)
                    {
                        context.CompileExpression(argument, requireValue: true);
                    }

                    context.instructions.Add(new InstructionBuilder(
                        KlibOpCode.Call,
                        ToLocation(token),
                        KlibMappingKind.Expression,
                        context.constantPool.GetStringIndex(token.Lexeme),
                        arguments.Count));
                    return;
                }

                if (!IsAtEnd() && !IsBinaryOrDelimiter(Current.Kind) &&
                    (context.builtIns.TryResolve(token.Lexeme, out _) || LooksLikeCallArgumentStart(Current.Kind)))
                {
                    var argumentTokens = ReadCallTail();
                    var arguments = SplitArguments(argumentTokens);
                    foreach (var argument in arguments)
                    {
                        context.CompileExpression(argument, requireValue: true);
                    }

                    context.instructions.Add(new InstructionBuilder(
                        KlibOpCode.Call,
                        ToLocation(token),
                        KlibMappingKind.Expression,
                        context.constantPool.GetStringIndex(token.Lexeme),
                        arguments.Count));
                    return;
                }

                if (context.TryResolveVariable(token.Lexeme, out var variableIndex))
                {
                    context.instructions.Add(new InstructionBuilder(KlibOpCode.LoadVar, ToLocation(token), KlibMappingKind.Expression, variableIndex));
                    return;
                }

                if (context.actorNames.Contains(token.Lexeme))
                {
                    var actorIndex = context.constantPool.GetActorReferenceIndex($"actor.{NormalizeIdentifier(token.Lexeme)}");
                    context.instructions.Add(new InstructionBuilder(KlibOpCode.PushConst, ToLocation(token), KlibMappingKind.Expression, actorIndex));
                    return;
                }

                context.instructions.Add(new InstructionBuilder(KlibOpCode.PushConst, ToLocation(token), KlibMappingKind.Expression, context.constantPool.GetStringConstantIndex(token.Lexeme)));
            }

            private void ParseArrayLiteral(Token token)
            {
                var count = 0;
                if (Match(TokenKind.CloseBracket))
                {
                    context.instructions.Add(new InstructionBuilder(KlibOpCode.ArrayNew, ToLocation(token), KlibMappingKind.Expression, 0));
                    return;
                }

                while (!IsAtEnd())
                {
                    ParseExpression();
                    count++;
                    if (Match(TokenKind.Comma))
                    {
                        continue;
                    }

                    Consume(TokenKind.CloseBracket);
                    break;
                }

                context.instructions.Add(new InstructionBuilder(KlibOpCode.ArrayNew, ToLocation(token), KlibMappingKind.Expression, count));
            }

            private List<IReadOnlyList<Token>> ReadArgumentsUntilCloseParen()
            {
                var arguments = new List<IReadOnlyList<Token>>();
                var start = position;
                var depth = 0;
                while (!IsAtEnd())
                {
                    if (Current.Kind == TokenKind.OpenParen)
                    {
                        depth++;
                    }
                    else if (Current.Kind == TokenKind.CloseParen)
                    {
                        if (depth == 0)
                        {
                            if (position > start)
                            {
                                arguments.Add(tokens.Skip(start).Take(position - start).ToArray());
                            }

                            Advance();
                            break;
                        }

                        depth--;
                    }
                    else if (Current.Kind == TokenKind.Comma && depth == 0)
                    {
                        arguments.Add(tokens.Skip(start).Take(position - start).ToArray());
                        Advance();
                        start = position;
                        continue;
                    }

                    position++;
                }

                return arguments;
            }

            private IReadOnlyList<Token> ReadCallTail()
            {
                var start = position;
                while (!IsAtEnd() && !IsBinaryOrDelimiter(Current.Kind))
                {
                    position++;
                }

                return tokens.Skip(start).Take(position - start).ToArray();
            }

            private void EmitNumber(Token token)
            {
                if (int.TryParse(token.Lexeme, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                {
                    context.instructions.Add(new InstructionBuilder(KlibOpCode.PushInt, ToLocation(token), KlibMappingKind.Expression, intValue));
                    return;
                }

                context.instructions.Add(new InstructionBuilder(
                    KlibOpCode.PushConst,
                    ToLocation(token),
                    KlibMappingKind.Expression,
                    context.constantPool.GetNumberConstantIndex(double.Parse(token.Lexeme, CultureInfo.InvariantCulture))));
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

            private void Consume(TokenKind kind)
            {
                if (!Match(kind))
                {
                    context.diagnostics.Add(context.Diagnostic("KES2016", Current.Line > 0 ? ToLocation(Current) : new SourceLocation(1, 1), $"Expected '{kind}' in expression."));
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

            private static SourceLocation ToLocation(Token token)
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

            private static bool LooksLikeCallArgumentStart(TokenKind kind)
            {
                return kind is TokenKind.Identifier or TokenKind.Keyword or TokenKind.NumberLiteral or TokenKind.StringLiteral
                    or TokenKind.OpenParen or TokenKind.OpenBracket or TokenKind.Bang or TokenKind.Minus;
            }
        }

        private static IReadOnlyList<IReadOnlyList<Token>> SplitArguments(IReadOnlyList<Token> tokens)
        {
            var arguments = new List<IReadOnlyList<Token>>();
            var index = 0;
            while (index < tokens.Count)
            {
                var start = index;
                var parenDepth = 0;
                var bracketDepth = 0;
                while (index < tokens.Count)
                {
                    var token = tokens[index];
                    if (token.Kind == TokenKind.OpenParen)
                    {
                        parenDepth++;
                    }
                    else if (token.Kind == TokenKind.CloseParen)
                    {
                        parenDepth--;
                    }
                    else if (token.Kind == TokenKind.OpenBracket)
                    {
                        bracketDepth++;
                    }
                    else if (token.Kind == TokenKind.CloseBracket)
                    {
                        bracketDepth--;
                    }
                    else if (parenDepth == 0 && bracketDepth == 0 && index > start && LooksLikeArgumentBoundary(token))
                    {
                        break;
                    }

                    index++;
                }

                arguments.Add(tokens.Skip(start).Take(index - start).ToArray());
            }

            return arguments;
        }

        private static bool LooksLikeArgumentBoundary(Token token)
        {
            return token.Kind is TokenKind.Identifier or TokenKind.Keyword or TokenKind.NumberLiteral or TokenKind.StringLiteral or TokenKind.OpenBracket
                && token.Column > 0;
        }
    }

    private class InstructionBuilder
    {
        public InstructionBuilder(KlibOpCode opCode, SourceLocation? source, KlibMappingKind mappingKind, params int[] operands)
        {
            OpCode = opCode;
            Source = source;
            MappingKind = mappingKind;
            Operands = operands.ToList();
        }

        public KlibOpCode OpCode { get; }

        public SourceLocation? Source { get; }

        public KlibMappingKind MappingKind { get; }

        public List<int> Operands { get; }

        public List<SelectCaseBuilder> SelectCases { get; } = [];

        public int Offset { get; set; }

        public int IndexHint { get; set; }

        public static InstructionBuilder Jump(string targetLabel, SourceLocation location)
        {
            return new JumpInstructionBuilder(KlibOpCode.Jump, targetLabel, location);
        }

        public static InstructionBuilder JumpFalse(string targetLabel, SourceLocation location)
        {
            return new JumpInstructionBuilder(KlibOpCode.JumpFalse, targetLabel, location);
        }

        public static InstructionBuilder Select(IReadOnlyList<SelectCaseBuilder> cases, SourceLocation location)
        {
            var builder = new SelectInstructionBuilder(location);
            builder.SelectCases.AddRange(cases);
            return builder;
        }

        public virtual int GetSize()
        {
            return 1 + (Operands.Count * 4);
        }

        public virtual void ResolveOffsets(
            IReadOnlyList<InstructionBuilder> allInstructions,
            IReadOnlyDictionary<string, int> labelInstructionIndexes,
            ConstantPoolBuilder constantPool,
            List<Diagnostic> diagnostics)
        {
        }

        public virtual KlibInstruction ToInstruction(int index)
        {
            return new KlibInstruction(index, Offset, OpCode, Operands.ToArray(), ToKlibSourceLocation(Source), MappingKind);
        }

        protected static KlibSourceLocation? ToKlibSourceLocation(SourceLocation? source)
        {
            return source is null ? null : new KlibSourceLocation(source.Value.Line, source.Value.Column);
        }
    }

    private sealed class JumpInstructionBuilder : InstructionBuilder
    {
        private readonly string targetLabel;

        public JumpInstructionBuilder(KlibOpCode opCode, string targetLabel, SourceLocation location)
            : base(opCode, location, KlibMappingKind.Statement, 0)
        {
            this.targetLabel = targetLabel;
        }

        public override void ResolveOffsets(
            IReadOnlyList<InstructionBuilder> allInstructions,
            IReadOnlyDictionary<string, int> labelInstructionIndexes,
            ConstantPoolBuilder constantPool,
            List<Diagnostic> diagnostics)
        {
            if (!labelInstructionIndexes.TryGetValue(targetLabel, out var targetInstructionIndex))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticLevel.Error,
                    "KES2017",
                    string.Empty,
                    Source?.Line ?? 1,
                    Source?.Column ?? 1,
                    $"Unresolved control target '{targetLabel}' during .klib compilation."));
                return;
            }

            var targetOffset = allInstructions[targetInstructionIndex].Offset;
            Operands[0] = targetOffset - (Offset + GetSize());
        }
    }

    private sealed class SelectInstructionBuilder : InstructionBuilder
    {
        public SelectInstructionBuilder(SourceLocation location)
            : base(KlibOpCode.Select, location, KlibMappingKind.Statement, 0)
        {
        }

        public override int GetSize()
        {
            return 1 + 4 + (SelectCases.Count * 8);
        }

        public override void ResolveOffsets(
            IReadOnlyList<InstructionBuilder> allInstructions,
            IReadOnlyDictionary<string, int> labelInstructionIndexes,
            ConstantPoolBuilder constantPool,
            List<Diagnostic> diagnostics)
        {
            Operands.Clear();
            Operands.Add(SelectCases.Count);
            var baseOffset = Offset + GetSize();
            foreach (var @case in SelectCases)
            {
                if (!labelInstructionIndexes.TryGetValue(@case.TargetLabel, out var targetInstructionIndex))
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticLevel.Error,
                        "KES2017",
                        string.Empty,
                        Source?.Line ?? 1,
                        Source?.Column ?? 1,
                        $"Unresolved control target '{@case.TargetLabel}' during .klib compilation."));
                    continue;
                }

                var targetOffset = allInstructions[targetInstructionIndex].Offset;
                @case.Offset = targetOffset - baseOffset;
            }
        }

        public override KlibInstruction ToInstruction(int index)
        {
            return new KlibInstruction(
                index,
                Offset,
                OpCode,
                Operands.ToArray(),
                ToKlibSourceLocation(Source),
                MappingKind,
                SelectCases.Select(@case => new KlibSelectCase(@case.TextIndex, @case.Offset)).ToArray());
        }
    }

    private sealed record SelectCaseBuilder(int TextIndex, string TargetLabel)
    {
        public int Offset { get; set; }
    }

    private sealed class ConstantPoolBuilder
    {
        private readonly List<KlibConstant> constants = [];
        private readonly Dictionary<string, int> stringIndexes = new(StringComparer.Ordinal);
        private readonly Dictionary<double, int> numberIndexes = [];
        private readonly Dictionary<string, int> actorReferenceIndexes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> localeKeyIndexes = new(StringComparer.Ordinal);

        public int GetStringIndex(string value)
        {
            if (stringIndexes.TryGetValue(value, out var existing))
            {
                return existing;
            }

            var index = constants.Count;
            constants.Add(new KlibConstant(KlibConstantKind.String, StringValue: value));
            stringIndexes[value] = index;
            return index;
        }

        public int GetStringConstantIndex(string value)
        {
            return GetStringIndex(value);
        }

        public int GetNumberConstantIndex(double value)
        {
            if (numberIndexes.TryGetValue(value, out var existing))
            {
                return existing;
            }

            var index = constants.Count;
            constants.Add(new KlibConstant(KlibConstantKind.Number, NumberValue: value));
            numberIndexes[value] = index;
            return index;
        }

        public int GetActorReferenceIndex(string value)
        {
            if (actorReferenceIndexes.TryGetValue(value, out var existing))
            {
                return existing;
            }

            var stringIndex = GetStringIndex(value);
            var index = constants.Count;
            constants.Add(new KlibConstant(KlibConstantKind.ActorRef, ReferenceIndex: stringIndex));
            actorReferenceIndexes[value] = index;
            return index;
        }

        public int GetLocaleKeyIndex(string value)
        {
            if (localeKeyIndexes.TryGetValue(value, out var existing))
            {
                return existing;
            }

            var stringIndex = GetStringIndex(value);
            var index = constants.Count;
            constants.Add(new KlibConstant(KlibConstantKind.LocaleKey, ReferenceIndex: stringIndex));
            localeKeyIndexes[value] = index;
            return index;
        }

        public string GetStringValue(int index)
        {
            return constants[index].StringValue
                ?? constants[constants[index].ReferenceIndex!.Value].StringValue
                ?? string.Empty;
        }

        public IReadOnlyList<KlibConstant> Build()
        {
            return constants.ToArray();
        }
    }
}
