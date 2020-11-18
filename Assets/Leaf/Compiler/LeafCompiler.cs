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
    public class LeafCompiler<TNode>
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
                PointToEnd(ioCompiler);
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
                m_EndPointers.Clear();
                m_Phase = Phase.Unstarted;
                m_Type = BlockType.Unassigned;
            }
        }

        #endregion // Types

        #region Consts

        static private readonly CleanedParseRules ParseRules = new CleanedParseRules();

        #endregion // Consts

        private readonly List<LeafInstruction> m_EmittedInstructions = new List<LeafInstruction>(32);
        private readonly Dictionary<StringHash32, string> m_EmittedLines = new Dictionary<StringHash32, string>(32);
        private readonly List<ILeafExpression<TNode>> m_EmittedExpressions = new List<ILeafExpression<TNode>>(32);
        private readonly StringBuilder m_TempStringBuilder = new StringBuilder(32);

        private ConditionalBlockLinker[] m_LinkerStack = new ConditionalBlockLinker[4];
        private int m_LinkerCount = 0;
        private ConditionalBlockLinker m_CurrentLinker;

        private string m_CurrentNodeId;
        private bool m_HasChoices;
        private int m_CurrentNodeLineOffset;

        private readonly ILeafCompilerPlugin<TNode> m_Plugin;
        private bool m_Verbose;
        private Func<string> m_RetrieveRoot;

        public LeafCompiler(ILeafCompilerPlugin<TNode> inPlugin)
        {
            if (inPlugin == null)
                throw new ArgumentNullException("inPlugin");

            m_Plugin = inPlugin;
        }

        /// <summary>
        /// Prepares to start compiling a module.
        /// </summary>
        public void StartModule(LeafNodePackage<TNode> inPackage, bool inbVerbose)
        {
            Reset();
            m_Verbose = inbVerbose;
            m_RetrieveRoot = inPackage.RootPath;
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
            m_CurrentNodeLineOffset = -(int) inStartPosition.LineNumber;
            m_EmittedInstructions.Clear();
        }

        /// <summary>
        /// Flushes the currently compiled instructions to the given node.
        /// </summary>
        public void FinishNode(TNode ioNode, BlockFilePosition inPosition)
        {
            if (m_LinkerCount > 0)
                throw new SyntaxException(inPosition, "Unclosed if/endif block");

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
        }

        /// <summary>
        /// Flushes the accumulated content and expressions to the given package.
        /// </summary>
        public void FinishModule(LeafNodePackage<TNode> ioPackage)
        {
            ioPackage.SetLines(m_EmittedLines);
            ioPackage.SetExpressions(m_EmittedExpressions.ToArray());
            
            m_EmittedLines.Clear();
            m_EmittedExpressions.Clear();
        }

        /// <summary>
        /// Resets compiler state.
        /// </summary>
        public void Reset()
        {
            m_EmittedInstructions.Clear();
            m_EmittedLines.Clear();
            m_EmittedExpressions.Clear();

            m_CurrentNodeId = null;
            m_HasChoices = false;
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
            
            StringHash32 lineCode = EmitLine(inFilePosition, inLine);
            EmitInstruction(LeafOpcode.RunLine, lineCode);
        }

        private bool TryProcessCommand(BlockFilePosition inPosition, StringSlice inLine)
        {
            if (!inLine.StartsWith('$'))
                return false;

            inLine = inLine.Substring(1);

            TagData data = TagData.Parse(inLine, ParseRules);

            // single instructions
            if (data.Id == "stop")
            {
                ProcessOptionalCondition(inPosition, data, LeafOpcode.Stop);
                return true;
            }

            if (data.Id == "yield")
            {
                EmitInstruction(LeafOpcode.Yield);
                return true;
            }

            if (data.Id == "return")
            {
                ProcessOptionalCondition(inPosition, data, LeafOpcode.ReturnFromNode);
                return true;
            }

            // goto/branch/loop
            if (data.Id == "goto")
            {
                ProcessGotoBranch(inPosition, data, LeafOpcode.GotoNode, LeafOpcode.GotoNodeIndirect);
                return true;
            }

            if (data.Id == "branch")
            {
                ProcessGotoBranch(inPosition, data, LeafOpcode.BranchNode, LeafOpcode.BranchNodeIndirect);
                return true;
            }

            if (data.Id == "loop")
            {
                ProcessOptionalCondition(inPosition, data, LeafOpcode.Loop);
                return true;
            }

            // set
            if (data.Id == "set")
            {
                if (data.Data.IsEmpty)
                    throw new SyntaxException(inPosition, "Expected expressions with set command");

                EmitExpressionSet(data.Data);
                return true;
            }

            // choice
            if (data.Id == "choice")
            {
                ProcessChoice(inPosition, data);
                return true;
            }

            // if statements
            if (data.Id == "if")
            {
                NewLinker().If(inPosition, data.Data, this);
                return true;
            }
            else if (data.Id == "elseif")
            {
                CurrentLinker(inPosition).ElseIf(inPosition, data.Data, this);
                return true;
            }
            else if (data.Id == "else")
            {
                CurrentLinker(inPosition).Else(inPosition, this);
                return true;
            }
            else if (data.Id == "endif")
            {
                CurrentLinker(inPosition).EndIf(inPosition, this);
                PopLinker();
                return true;
            }

            // while statements
            if (data.Id == "while")
            {
                NewLinker().While(inPosition, data.Data, this);
                return true;
            }
            else if (data.Id == "break")
            {
                CurrentLinker(inPosition).Break(inPosition, this);
                return true;
            }
            else if (data.Id == "continue")
            {
                CurrentLinker(inPosition).Continue(inPosition, this);
                return true;
            }
            else if (data.Id == "endwhile")
            {
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
            StringHash32 key = GenerateLineCode(inPosition, m_CurrentNodeId, m_CurrentNodeLineOffset);
            m_EmittedLines.Add(key, inLine.ToString());
            return key;
        }

        private void EmitExpressionCall(StringSlice inExpression)
        {
            uint key = (uint) m_EmittedExpressions.Count;
            m_EmittedExpressions.Add(m_Plugin.CompileExpression(inExpression));
            EmitInstruction(LeafOpcode.EvaluateExpression, key);
        }

        private void EmitExpressionSet(StringSlice inExpression)
        {
            uint key = (uint) m_EmittedExpressions.Count;
            m_EmittedExpressions.Add(m_Plugin.CompileExpression(inExpression));
            EmitInstruction(LeafOpcode.SetFromExpression, key);
        }

        #endregion // Emit

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