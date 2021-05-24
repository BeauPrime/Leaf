/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    24 Oct 2020
 * 
 * File:    LeafCompiler.cs
 * Purpose: Compilation environment for LeafNodes.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BeauUtil;
using BeauUtil.Blocks;
using BeauUtil.Tags;
using BeauUtil.Variants;
using Leaf.Runtime;

namespace Leaf.Compiler
{
    /// <summary>
    /// Compiles leaf nodes into instructions.
    /// </summary>
    public sealed class LeafCompiler<TNode>
        where TNode : LeafNode
    {
        #region Types

        private class SyntaxException : Exception
        {
            public SyntaxException(BlockFilePosition inPosition, string inMessage)
                : base(string.Format("Syntax Error at {0}: {1}", inPosition, inMessage))
            { }

            public SyntaxException(BlockFilePosition inPosition, string inMessage, params object[] inArgs)
                : this(inPosition, string.Format(inMessage, inArgs))
            { }
        }

        private class CleanedParseRules : IDelimiterRules
        {
            public string TagStartDelimiter { get { return string.Empty; } }
            public string TagEndDelimiter { get { return string.Empty; } }
            public char[] TagDataDelimiters { get { return TagData.DefaultDataDelimiters; } }
            public char RegionCloseDelimiter { get { return (char) 0; } }

            public bool RichText { get { return true; } }
            public IEnumerable<string> AdditionalRichTextTags { get { return null; } }
        }

        private class ConditionalBlockLinker
        {
            private enum BlockType
            {
                Unassigned,
                If,
                While
            }

            private enum Phase
            {
                Unstarted,
                Started,
                Continued,
                FinalElse
            }

            private Phase m_Phase;
            private BlockType m_Type;
            private int m_NextPointer = -1;
            private int m_StartPointer = -1;
            private int m_ConditionalEndPointer = -1;
            private readonly List<int> m_EndPointers = new List<int>(4);

            #region If

            /// <summary>
            /// Handles an if statement.
            /// </summary>
            public void If(BlockFilePosition inPosition, StringSlice inExpression, LeafCompiler<TNode> ioCompiler)
            {
                if (m_Phase != Phase.Unstarted)
                    throw new SyntaxException(inPosition, "If statement in an unexpected location");
                
                m_Phase = Phase.Started;
                m_Type = BlockType.If;
                
                EmitExpressionCheck(inExpression, ioCompiler);
            }

            /// <summary>
            /// Handles an elseif statement
            /// </summary>
            public void ElseIf(BlockFilePosition inPosition, StringSlice inExpression, LeafCompiler<TNode> ioCompiler)
            {
                if (m_Type != BlockType.If)
                    throw new SyntaxException(inPosition, "elseIf while not in an if block");

                switch(m_Phase)
                {
                    case Phase.Unstarted:
                        throw new SyntaxException(inPosition, "ElseIf without corresponding initial if statement");
                    case Phase.FinalElse:
                        throw new SyntaxException(inPosition, "ElseIf cannot come after a final Else statement");
                    case Phase.Started:
                        m_Phase = Phase.Continued;
                        break;
                }

                PointToEnd(ioCompiler);
                Advance(ioCompiler);
                EmitExpressionCheck(inExpression, ioCompiler);
            }

            /// <summary>
            /// Handles an else statement
            /// </summary>
            public void Else(BlockFilePosition inPosition, LeafCompiler<TNode> ioCompiler)
            {
                if (m_Type != BlockType.If)
                    throw new SyntaxException(inPosition, "else while not in an if block");

                switch(m_Phase)
                {
                    case Phase.Unstarted:
                        throw new SyntaxException(inPosition, "Else without corresponding initial if statement");
                    case Phase.FinalElse:
                        throw new SyntaxException(inPosition, "Else cannot come after a final Else statement");
                    case Phase.Started:
                    case Phase.Continued:
                        m_Phase = Phase.FinalElse;
                        break;
                }

                PointToEnd(ioCompiler);
                Advance(ioCompiler);
            }

            /// <summary>
            /// Handles an endif statement
            /// </summary>
            public void EndIf(BlockFilePosition inPosition, LeafCompiler<TNode> ioCompiler)
            {
                if (m_Type != BlockType.If)
                    throw new SyntaxException(inPosition, "endif while not in an if block");

                switch(m_Phase)
                {
                    case Phase.Unstarted:
                        throw new SyntaxException(inPosition, "EndIf without corresponding initial if statement");
                }

                m_Phase = Phase.Unstarted;
                Advance(ioCompiler);

                LinkEndPointers(ioCompiler);
            }

            #endregion // If

            #region While

            /// <summary>
            /// Handles a while statement.
            /// </summary>
            public void While(BlockFilePosition inPosition, StringSlice inExpression, LeafCompiler<TNode> ioCompiler)
            {
                if (m_Phase != Phase.Unstarted)
                    throw new SyntaxException(inPosition, "while statement in an unexpected location");
                
                m_Phase = Phase.Started;
                m_Type = BlockType.While;
                m_StartPointer = ioCompiler.InstructionCount;
                
                EmitExpressionCheckBlock(inExpression, ioCompiler);
            }

            /// <summary>
            /// Handles a break statement
            /// </summary>
            public void Break(BlockFilePosition inPosition, LeafCompiler<TNode> ioCompiler)
            {
                if (m_Type != BlockType.While)
                    throw new SyntaxException(inPosition, "break while not in a while block");

                switch(m_Phase)
                {
                    case Phase.Unstarted:
                        throw new SyntaxException(inPosition, "Break without corresponding initial while statement");
                }

                PointToEnd(ioCompiler);
            }

            /// <summary>
            /// Handles a continue statement
            /// </summary>
            public void Continue(BlockFilePosition inPosition, LeafCompiler<TNode> ioCompiler)
            {
                if (m_Type != BlockType.While)
                    throw new SyntaxException(inPosition, "continue while not in a while block");

                switch(m_Phase)
                {
                    case Phase.Unstarted:
                        throw new SyntaxException(inPosition, "Continue without corresponding initial while statement");
                }

                PointToStart(ioCompiler);
            }

            /// <summary>
            /// Handles an endwhile statement
            /// </summary>
            public void EndWhile(BlockFilePosition inPosition, LeafCompiler<TNode> ioCompiler)
            {
                if (m_Type != BlockType.While)
                    throw new SyntaxException(inPosition, "endwhile while not in a while block");

                switch(m_Phase)
                {
                    case Phase.Unstarted:
                        throw new SyntaxException(inPosition, "EndWhile without corresponding initial while statement");
                }

                m_Phase = Phase.Unstarted;
                PointToStart(ioCompiler);

                LinkEndPointers(ioCompiler);
            }

            #endregion // While

            private void LinkEndPointers(LeafCompiler<TNode> ioCompiler)
            {
                if (m_ConditionalEndPointer >= 0)
                {
                    LeafInstruction inst = ioCompiler.m_EmittedInstructions[m_ConditionalEndPointer];
                    int jump = ioCompiler.InstructionCount - m_ConditionalEndPointer;
                    inst.SetArg(jump);
                    ioCompiler.m_EmittedInstructions[m_ConditionalEndPointer] = inst;

                    m_ConditionalEndPointer = -1;
                }

                for(int i = m_EndPointers.Count - 1; i >= 0; --i)
                {
                    int idx = m_EndPointers[i];
                    LeafInstruction inst = ioCompiler.m_EmittedInstructions[idx];
                    int jump = ioCompiler.InstructionCount - idx;
                    inst.SetArg(jump);
                    ioCompiler.m_EmittedInstructions[idx] = inst;
                }

                m_EndPointers.Clear();
            }

            private void EmitExpressionCheck(StringSlice inExpression, LeafCompiler<TNode> ioCompiler)
            {
                ioCompiler.EmitExpressionCall(inExpression);
                ioCompiler.EmitInstruction(LeafOpcode.JumpIfFalse, -1);
                m_NextPointer = ioCompiler.InstructionCount - 1;
            }

            private void EmitExpressionCheckBlock(StringSlice inExpression, LeafCompiler<TNode> ioCompiler)
            {
                ioCompiler.EmitExpressionCall(inExpression);
                ioCompiler.EmitInstruction(LeafOpcode.JumpIfFalse, -1);
                m_ConditionalEndPointer = ioCompiler.InstructionCount - 1;
            }

            private void Advance(LeafCompiler<TNode> ioCompiler)
            {
                if (m_NextPointer >= 0)
                {
                    LeafInstruction inst = ioCompiler.m_EmittedInstructions[m_NextPointer];
                    int jump = ioCompiler.InstructionCount - m_NextPointer;
                    inst.SetArg(jump);
                    ioCompiler.m_EmittedInstructions[m_NextPointer] = inst;
                    m_NextPointer = -1;
                }
            }

            private void PointToEnd(LeafCompiler<TNode> ioCompiler)
            {
                ioCompiler.EmitInstruction(LeafOpcode.Jump, -1);
                m_EndPointers.Add(ioCompiler.InstructionCount - 1);
            }

            private void PointToStart(LeafCompiler<TNode> ioCompiler)
            {
                int jump = m_StartPointer - ioCompiler.InstructionCount;
                ioCompiler.EmitInstruction(LeafOpcode.Jump, jump);
            }

            /// <summary>
            /// Clears the block.
            /// </summary>
            public void Clear()
            {
                m_NextPointer = -1;
                m_StartPointer = -1;
                m_ConditionalEndPointer = -1;
                m_EndPointers.Clear();
                m_Phase = Phase.Unstarted;
                m_Type = BlockType.Unassigned;
            }
        }

        private struct InvocationCache
        {
            public uint Key;
            public StringHash32 Target;
        }

        #endregion // Types

        #region Consts

        static private readonly CleanedParseRules ParseRules = new CleanedParseRules();

        #endregion // Consts

        private readonly List<LeafInstruction> m_EmittedInstructions = new List<LeafInstruction>(32);
        private readonly Dictionary<StringHash32, string> m_EmittedLines = new Dictionary<StringHash32, string>(32);

        private readonly Dictionary<StringSlice, uint> m_ExpressionReuseMap = new Dictionary<StringSlice, uint>(32);
        private readonly Dictionary<StringSlice, InvocationCache> m_InvocationReuseMap = new Dictionary<StringSlice, InvocationCache>(32);

        private readonly List<ILeafExpression<TNode>> m_EmittedExpressions = new List<ILeafExpression<TNode>>(32);
        private readonly List<ILeafInvocation<TNode>> m_EmittedInvocations = new List<ILeafInvocation<TNode>>(32);

        private readonly StringBuilder m_TempStringBuilder = new StringBuilder(32);

        private readonly StringBuilder m_ContentBuilder;
        private BlockFilePosition m_ContentStartPosition;

        private ConditionalBlockLinker[] m_LinkerStack = new ConditionalBlockLinker[4];
        private int m_LinkerCount = 0;
        private ConditionalBlockLinker m_CurrentLinker;

        private string m_CurrentNodeId;
        private bool m_HasChoices;
        private bool m_HasForks;
        private int m_CurrentNodeLineOffset;

        private readonly ILeafCompilerPlugin<TNode> m_Plugin;
        private bool m_Verbose;
        private Func<string> m_RetrieveRoot;

        public LeafCompiler(ILeafCompilerPlugin<TNode> inPlugin)
        {
            if (inPlugin == null)
                throw new ArgumentNullException("inPlugin");

            m_Plugin = inPlugin;

            if (m_Plugin.CollapseContent)
                m_ContentBuilder = new StringBuilder(256);
            else
                m_ContentBuilder = new StringBuilder(1);
        }

        /// <summary>
        /// Prepares to start compiling a module.
        /// </summary>
        public void StartModule(LeafNodePackage<TNode> inPackage, bool inbVerbose)
        {
            Reset();
            m_Verbose = inbVerbose;
            m_RetrieveRoot = inPackage.RootPath;

            if (m_Verbose)
            {
                UnityEngine.Debug.LogFormat("[LeafCompiler] Starting compilation for module '{0}'", inPackage.Name());
            }
        }

        /// <summary>
        /// Prepares to start compiling a node with the given id and starting position.
        /// </summary>
        public void StartNode(string inNodeId, BlockFilePosition inStartPosition)
        {
            if (!LeafNode.IsValidIdentifier(inNodeId))
                throw new SyntaxException(inStartPosition, "Invalid node id '{0}'", inNodeId);

            m_CurrentNodeId = inNodeId;
            m_HasChoices = false;
            m_HasForks = false;
            m_CurrentNodeLineOffset = -(int) inStartPosition.LineNumber;
            m_EmittedInstructions.Clear();
        }

        /// <summary>
        /// Flushes the currently compiled instructions to the given node.
        /// </summary>
        public void FinishNode(TNode ioNode, BlockFilePosition inPosition)
        {
            if (m_LinkerCount > 0)
                throw new SyntaxException(inPosition, "Unclosed linker block (if/endif or while/endwhile)");

            FlushContent();

            if (m_HasForks)
            {
                EmitInstruction(LeafOpcode.JoinForks);
            }

            if (m_HasChoices)
            {
                EmitInstruction(LeafOpcode.ShowChoices);
                EmitInstruction(LeafOpcode.GotoNodeIndirect);
            }

            if (m_Verbose)
            {
                UnityEngine.Debug.LogFormat("[LeafCompiler] Emitting instructions for node '{0}':\n{1}",
                    m_CurrentNodeId, LeafInstruction.ToDebugString(m_EmittedInstructions));
            }

            ioNode.SetInstructions(m_EmittedInstructions.ToArray());
            m_EmittedInstructions.Clear();

            m_CurrentNodeId = null;
            m_HasChoices = false;
            m_HasForks = false;
        }

        /// <summary>
        /// Flushes the accumulated content and expressions to the given package.
        /// </summary>
        public void FinishModule(LeafNodePackage<TNode> ioPackage)
        {
            ioPackage.SetLines(m_EmittedLines);
            ioPackage.SetExpressions(m_EmittedExpressions.ToArray());
            ioPackage.SetInvocations(m_EmittedInvocations.ToArray());
            
            if (m_Verbose)
            {
                m_TempStringBuilder.Length = 0;

                m_TempStringBuilder.Append("[LeafCompiler] Finished compiling module '")
                    .Append(ioPackage.Name()).Append('\'');
                m_TempStringBuilder.Append("\nEmitted ").Append(m_EmittedLines.Count).Append(" text lines");
                m_TempStringBuilder.Append("\nEmitted ").Append(m_EmittedExpressions.Count).Append(" expressions");
                m_TempStringBuilder.Append("\nEmitted ").Append(m_EmittedInvocations.Count).Append(" invocations");

                UnityEngine.Debug.LogFormat(m_TempStringBuilder.Flush());
            }

            m_EmittedLines.Clear();
            m_EmittedExpressions.Clear();
            m_EmittedInstructions.Clear();

            m_ExpressionReuseMap.Clear();
            m_InvocationReuseMap.Clear();
        }

        /// <summary>
        /// Resets compiler state.
        /// </summary>
        public void Reset()
        {
            m_EmittedInstructions.Clear();
            m_EmittedLines.Clear();
            m_EmittedExpressions.Clear();
            m_EmittedInvocations.Clear();
            m_ExpressionReuseMap.Clear();
            m_InvocationReuseMap.Clear();

            m_CurrentNodeId = null;
            m_HasChoices = false;
            m_HasForks = false;
            m_CurrentNodeLineOffset = 0;

            m_LinkerCount = 0;
        }

        #region Process

        /// <summary>
        /// Processes the given line into instructions.
        /// </summary>
        public void Process(BlockFilePosition inFilePosition, StringSlice inLine)
        {
            StringSlice beginningTrimmed = inLine.TrimStart(TagData.MinimalWhitespaceChars);

            if (m_EmittedInstructions.Count == 0)
                inLine = beginningTrimmed;

            if (inLine.IsEmpty)
                return;

            if (TryProcessCommand(inFilePosition, beginningTrimmed))
                return;

            ProcessContent(inFilePosition, inLine);
        }

        private bool TryProcessCommand(BlockFilePosition inPosition, StringSlice inLine)
        {
            if (!inLine.StartsWith('$'))
                return false;

            inLine = inLine.Substring(1);

            TagData data = TagData.Parse(inLine, ParseRules);

            // single instructions
            if (data.Id == LeafTokens.Stop)
            {
                FlushContent();
                ProcessOptionalCondition(inPosition, data, LeafOpcode.Stop);
                return true;
            }

            if (data.Id == LeafTokens.Yield)
            {
                FlushContent();
                EmitInstruction(LeafOpcode.Yield);
                return true;
            }

            if (data.Id == LeafTokens.Return)
            {
                FlushContent();
                ProcessOptionalCondition(inPosition, data, LeafOpcode.ReturnFromNode);
                return true;
            }

            if (data.Id == LeafTokens.Choose)
            {
                if (!m_HasChoices)
                    throw new SyntaxException(inPosition, "choose must come after at least one choice statement");

                FlushContent();

                EmitInstruction(LeafOpcode.ShowChoices);
                if (data.Data.IsEmpty || data.Data == LeafTokens.Goto)
                    EmitInstruction(LeafOpcode.GotoNodeIndirect);
                else if (data.Data == LeafTokens.Branch)
                    EmitInstruction(LeafOpcode.BranchNodeIndirect);
                else
                    throw new SyntaxException(inPosition, "unrecognized argument to choose statement '{0}' - must be either goto or branch", data.Data);

                m_HasChoices = false;

                return true;
            }

            // goto/branch/fork/loop
            if (data.Id == LeafTokens.Goto)
            {
                FlushContent();
                ProcessGotoBranch(inPosition, data, LeafOpcode.GotoNode, LeafOpcode.GotoNodeIndirect);
                return true;
            }

            if (data.Id == LeafTokens.Branch)
            {
                FlushContent();
                ProcessGotoBranch(inPosition, data, LeafOpcode.BranchNode, LeafOpcode.BranchNodeIndirect);
                return true;
            }

            if (data.Id == LeafTokens.Fork)
            {
                FlushContent();
                ProcessGotoBranch(inPosition, data, LeafOpcode.ForkNode, LeafOpcode.ForkNodeIndirect);
                m_HasForks = true;
                return true;
            }

            if (data.Id == LeafTokens.Start)
            {
                FlushContent();
                ProcessGotoBranch(inPosition, data, LeafOpcode.ForkNodeUntracked, LeafOpcode.ForkNodeIndirectUntracked);
                return true;
            }

            if (data.Id == LeafTokens.Loop)
            {
                FlushContent();
                ProcessOptionalCondition(inPosition, data, LeafOpcode.Loop);
                return true;
            }

            if (data.Id == LeafTokens.Join)
            {
                if (!m_HasForks)
                    throw new SyntaxException(inPosition, "join must come after at least one fork statement");
                
                FlushContent();
                EmitInstruction(LeafOpcode.JoinForks);
                m_HasForks = false;
                return true;
            }

            // set
            if (data.Id == LeafTokens.Set)
            {
                if (data.Data.IsEmpty)
                    throw new SyntaxException(inPosition, "set must be provided an expression");

                FlushContent();
                EmitExpressionSet(data.Data);
                return true;
            }

            // invoke/tell
            if (data.Id == LeafTokens.Call)
            {
                if (data.Data.IsEmpty)
                    throw new SyntaxException(inPosition, "call must be provided a method");

                FlushContent();
                ProcessInvocation(inPosition, data);
                return true;
            }

            // choice
            if (data.Id == LeafTokens.Choice)
            {
                FlushContent();
                ProcessChoice(inPosition, data);
                return true;
            }

            // if statements
            if (data.Id == LeafTokens.If)
            {
                FlushContent();
                NewLinker().If(inPosition, data.Data, this);
                return true;
            }
            else if (data.Id == LeafTokens.ElseIf)
            {
                FlushContent();
                CurrentLinker(inPosition).ElseIf(inPosition, data.Data, this);
                return true;
            }
            else if (data.Id == LeafTokens.Else)
            {
                FlushContent();
                CurrentLinker(inPosition).Else(inPosition, this);
                return true;
            }
            else if (data.Id == LeafTokens.EndIf)
            {
                FlushContent();
                CurrentLinker(inPosition).EndIf(inPosition, this);
                PopLinker();
                return true;
            }

            // while statements
            if (data.Id == LeafTokens.While)
            {
                FlushContent();
                NewLinker().While(inPosition, data.Data, this);
                return true;
            }
            else if (data.Id == LeafTokens.Break)
            {
                FlushContent();
                CurrentLinker(inPosition).Break(inPosition, this);
                return true;
            }
            else if (data.Id == LeafTokens.Continue)
            {
                FlushContent();
                CurrentLinker(inPosition).Continue(inPosition, this);
                return true;
            }
            else if (data.Id == LeafTokens.EndWhile)
            {
                FlushContent();
                CurrentLinker(inPosition).EndWhile(inPosition, this);
                PopLinker();
                return true;
            }

            return false;
        }

        private void ProcessGotoBranch(BlockFilePosition inPosition, TagData inData, LeafOpcode inDirect, LeafOpcode inIndirect)
        {
            // Syntax
            // goto node
            // goto [node expression]
            // goto node, expression
            // goto [node expression], expression
            // branch node
            // branch [node expression]
            // branch node, expression
            // branch [node expression], expression
            // fork node
            // fork [node expression]
            // fork node, expression
            // fork [node expression], expression

            StringSlice nodeId, expression;
            SplitNodeExpression(inData.Data, out nodeId, out expression);

            if (!expression.IsEmpty)
            {
                EmitExpressionCall(expression);
                EmitInstruction(LeafOpcode.JumpIfFalse, 2);
            }

            if (nodeId.IsEmpty)
                throw new SyntaxException(inPosition, "goto or branch commands cannot have empty target");

            StringSlice nodeExp;
            if (IsIndirect(nodeId, out nodeExp))
            {
                EmitExpressionCall(nodeExp);
                EmitInstruction(inIndirect);
            }
            else
            {
                nodeId = ProcessNodeId(nodeId);

                if (!LeafNode.IsValidIdentifier(nodeId))
                    throw new SyntaxException(inPosition, "node identifier '{0}' is not a valid identifier", nodeId);
                    
                EmitInstruction(inDirect, nodeId.Hash32());
            }
        }

        private void ProcessOptionalCondition(BlockFilePosition inPosition, TagData inData, LeafOpcode inOpcode)
        {
            if (!inData.Data.IsEmpty)
            {
                EmitExpressionCall(inData.Data);
                EmitInstruction(LeafOpcode.JumpIfFalse, 2);
            }

            EmitInstruction(inOpcode);
        }

        private void ProcessChoice(BlockFilePosition inPosition, TagData inData)
        {
            // Syntax
            // choice node; text
            // choice [node expression]; text
            // choice node, expression; text
            // choice [node expression], expression; text

            StringSlice args, content;
            SplitArgsContent(inData.Data, out args, out content);

            if (content.IsEmpty)
                throw new SyntaxException(inPosition, "choice commands must have ; followed by at least one non-whitespace character");

            StringSlice nodeId, expression;
            SplitNodeExpression(args, out nodeId, out expression);

            // push node id
            if (nodeId.IsEmpty)
                throw new SyntaxException(inPosition, "choice commands cannot have empty target");

            StringSlice nodeExp;
            if (IsIndirect(nodeId, out nodeExp))
            {
                EmitExpressionCall(nodeExp);
            }
            else
            {
                nodeId = ProcessNodeId(nodeId);

                if (!LeafNode.IsValidIdentifier(nodeId))
                    throw new SyntaxException(inPosition, "node identifier '{0}' is not a valid identifier", nodeId);
                    
                EmitInstruction(LeafOpcode.PushValue, nodeId.Hash32());
            }

            // push line code
            StringHash32 lineCode = EmitLine(inPosition, content);
            EmitInstruction(LeafOpcode.PushValue, lineCode);

            // push bool
            if (!expression.IsEmpty)
            {
                EmitExpressionCall(expression);
            }
            else
            {
                EmitInstruction(LeafOpcode.PushValue, Variant.True);
            }

            EmitInstruction(LeafOpcode.AddChoiceOption);

            m_HasChoices = true;
        }

        private void ProcessInvocation(BlockFilePosition inPosition, TagData inData)
        {
            StringHash32 target;
            uint key;

            InvocationCache cache;
            if (!m_InvocationReuseMap.TryGetValue(inData.Data, out cache))
            {
                key = (uint) m_EmittedInvocations.Count;

                StringSlice method, args, targetSlice;
                SplitMethodArgs(inData.Data, out method, out args);
                SplitTargetMethod(method, out targetSlice, out method);

                target = targetSlice;

                m_EmittedInvocations.Add(m_Plugin.CompileInvocation(method, args));
                m_InvocationReuseMap.Add(inData.Data, new InvocationCache()
                {
                    Target = target,
                    Key = key
                });
            }
            else
            {
                target = cache.Target;
                key = cache.Key;
            }

            if (target.IsEmpty)
            {
                EmitInstruction(LeafOpcode.Invoke, key);
            }
            else
            {
                EmitInstruction(LeafOpcode.PushValue, target);
                EmitInstruction(LeafOpcode.InvokeWithTarget, key);
            }

            return;

            // if (inbAllowTarget)
            // {
            //     StringSlice target = StringSlice.Empty;

            //     SplitTargetInvocation(invocation, out target, out invocation);
            //     if (target.IsEmpty)
            //         throw new SyntaxException(inPosition, "Target must be specified");

            //     StringSlice targetExp;
            //     if (IsIndirect(target, out targetExp))
            //     {
            //         EmitExpressionCall(targetExp);
            //     }
            //     else
            //     {
            //         EmitInstruction(LeafOpcode.PushValue, target.Hash32());
            //     }

            //     EmitInvoke(invocation, LeafOpcode.InvokeWithTarget);
            // }
            // else
            // {
            //     EmitInvoke(invocation, LeafOpcode.Invoke);
            // }

            // if (target.IsEmpty)
            // {
            //     EmitInvoke(invocation, LeafOpcode.Invoke)
            // }
        }

        #endregion // Process

        #region Emit

        private int InstructionCount
        {
            get { return m_EmittedInstructions.Count; }
        }

        private void EmitInstruction(LeafOpcode inOpcode, Variant inArgument = default(Variant))
        {
            m_EmittedInstructions.Add(new LeafInstruction(inOpcode, inArgument));
        }

        private StringHash32 EmitLine(BlockFilePosition inPosition, StringSlice inLine)
        {
            StringHash32 key = default(StringHash32);
            int lineCodeIdx = inLine.IndexOf("$[");
            if (lineCodeIdx >= 0)
            {
                int end = inLine.IndexOf(']', lineCodeIdx);
                if (end >= 0)
                {
                    string staticLineCode = inLine.Substring(lineCodeIdx + 2, end - lineCodeIdx - 2).Trim().ToString();
                    inLine = inLine.Substring(0, lineCodeIdx).TrimEnd();

                    uint hexKey;
                    if (uint.TryParse(staticLineCode, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hexKey))
                    {
                        key = new StringHash32(hexKey);
                    }
                    else
                    {
                        key = staticLineCode;
                    }
                }
            }
            if (key.IsEmpty)
            {
                key = GenerateLineCode(inPosition, m_CurrentNodeId, m_CurrentNodeLineOffset);
            }
            m_EmittedLines.Add(key, inLine.ToString());
            return key;
        }

        private void EmitExpressionCall(StringSlice inExpression)
        {
            uint key;
            if (!m_ExpressionReuseMap.TryGetValue(inExpression, out key))
            {
                key = (uint) m_EmittedExpressions.Count;
                m_EmittedExpressions.Add(m_Plugin.CompileExpression(inExpression));
                m_ExpressionReuseMap.Add(inExpression, key);
            }
            EmitInstruction(LeafOpcode.EvaluateExpression, key);
        }

        private void EmitExpressionSet(StringSlice inExpression)
        {
            uint key;
            if (!m_ExpressionReuseMap.TryGetValue(inExpression, out key))
            {
                key = (uint) m_EmittedExpressions.Count;
                m_EmittedExpressions.Add(m_Plugin.CompileExpression(inExpression));
                m_ExpressionReuseMap.Add(inExpression, key);
            }

            EmitInstruction(LeafOpcode.SetFromExpression, key);
        }

        #endregion // Emit

        #region Content

        private void ProcessContent(BlockFilePosition inPosition, StringSlice inLine)
        {
            if (!m_Plugin.CollapseContent)
            {
                StringHash32 lineCode = EmitLine(inPosition, inLine);
                EmitInstruction(LeafOpcode.RunLine, lineCode);
                return;
            }

            if (m_ContentBuilder.Length > 0)
            {
                m_ContentBuilder.Append('\n');
            }
            else
            {
                m_ContentStartPosition = inPosition;
            }

            inLine.AppendTo(m_ContentBuilder);
        }

        private void FlushContent()
        {
            if (m_ContentBuilder.Length > 0)
            {
                string text = m_ContentBuilder.Flush();
                
                StringHash32 lineCode = EmitLine(m_ContentStartPosition, text);
                EmitInstruction(LeafOpcode.RunLine, lineCode);

                m_ContentStartPosition = default(BlockFilePosition);
            }
        }

        #endregion // Content

        #region Sequence Linker

        private ConditionalBlockLinker NewLinker()
        {
            if (m_LinkerCount >= m_LinkerStack.Length)
            {
                Array.Resize(ref m_LinkerStack, m_LinkerStack.Length + 2);
            }

            int idx = m_LinkerCount++;
            ConditionalBlockLinker linker = m_LinkerStack[idx];
            if (linker == null)
            {
                linker = m_LinkerStack[idx] = new ConditionalBlockLinker();
            }
            linker.Clear();

            m_CurrentLinker = linker;
            return linker;
        }

        private ConditionalBlockLinker CurrentLinker(BlockFilePosition inPosition)
        {
            if (m_CurrentLinker == null)
                throw new SyntaxException(inPosition, "elseif, else, or endif statement without a corresponding if");

            return m_CurrentLinker;
        }

        private void PopLinker()
        {
            if (m_CurrentLinker == null)
                throw new InvalidOperationException("Attempting to pop null linker");
            
            m_CurrentLinker.Clear();
            if (--m_LinkerCount > 0)
                m_CurrentLinker = m_LinkerStack[m_LinkerCount - 1];
            else
                m_CurrentLinker = null;
        }

        #endregion // Sequence Linker

        #region Utilities

        private StringSlice ProcessNodeId(StringSlice inNodeId)
        {
            if (inNodeId.StartsWith(m_Plugin.PathSeparator))
            {
                return LeafNode.AssembleFullId(m_TempStringBuilder, m_RetrieveRoot(), inNodeId.Substring(1), m_Plugin.PathSeparator);
            }

            return inNodeId;
        }

        static private void SplitNodeExpression(StringSlice inData, out StringSlice outNodeId, out StringSlice outExpression)
        {
            int commaIdx = inData.IndexOf(',');
            if (commaIdx >= 0)
            {
                outNodeId = inData.Substring(0, commaIdx).TrimEnd(TagData.MinimalWhitespaceChars);
                outExpression = inData.Substring(commaIdx + 1).TrimStart(TagData.MinimalWhitespaceChars);
            }
            else
            {
                outNodeId = inData;
                outExpression = StringSlice.Empty;
            }
        }

        static private void SplitTargetMethod(StringSlice inData, out StringSlice outTarget, out StringSlice outMethod)
        {
            int indirectIndex = inData.IndexOf("->");
            if (indirectIndex >= 0)
            {
                outTarget = inData.Substring(0, indirectIndex).TrimEnd(TagData.MinimalWhitespaceChars);
                outMethod = inData.Substring(indirectIndex + 2).TrimStart(TagData.MinimalWhitespaceChars);
            }
            else
            {
                outTarget = StringSlice.Empty;
                outMethod = inData;
            }
        }

        static private void SplitMethodArgs(StringSlice inData, out StringSlice outMethod, out StringSlice outArgs)
        {
            TagData data = TagData.Parse(inData, ParseRules);
            outMethod = data.Id;
            outArgs = data.Data;
        }

        static private void SplitArgsContent(StringSlice inData, out StringSlice outArgs, out StringSlice outContent)
        {
            int semiIdx = inData.IndexOf(';');
            if (semiIdx >= 0)
            {
                outArgs = inData.Substring(0, semiIdx).TrimEnd(TagData.MinimalWhitespaceChars);
                outContent = inData.Substring(semiIdx + 1).TrimStart(TagData.MinimalWhitespaceChars);
            }
            else
            {
                outArgs = inData;
                outContent = StringSlice.Empty;
            }
        }

        static private bool IsIndirect(StringSlice inValue, out StringSlice outValue)
        {
            if (inValue.StartsWith('[') && inValue.EndsWith(']'))
            {
                outValue = inValue.Substring(1, inValue.Length - 2).Trim(TagData.MinimalWhitespaceChars);
                return !outValue.IsEmpty;
            }

            outValue = StringSlice.Empty;
            return false;
        }

        /// <summary>
        /// Generates a line code for the given position, node, and line type.
        /// </summary>
        static public StringHash32 GenerateLineCode(BlockFilePosition inFilePosition, string inNodeId, int inLineOffset = 0)
        {
            return string.Format("{0}|{1}:{2}", inFilePosition.FileName, inNodeId, inFilePosition.LineNumber + inLineOffset);
        }

        #endregion // Utilities
    }
}