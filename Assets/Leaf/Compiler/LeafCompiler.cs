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
            private enum Phase
            {
                Unstarted,
                Started,
                Continued,
                FinalElse
            }

            private Phase m_Phase;
            private int m_NextPointer = -1;
            private readonly List<int> m_EndPointers = new List<int>(4);

            /// <summary>
            /// Advances the block and points the next pointer to the current instruction.
            /// </summary>
            public void Advance(int inCurrent, List<LeafInstruction> ioInstructions)
            {
                LeafInstruction inst = ioInstructions[m_NextPointer];
                int jump = inCurrent - m_NextPointer;
                inst.SetArg(jump);
                ioInstructions[m_NextPointer] = inst;
                m_NextPointer = -1;
            }

            /// <summary>
            /// Sets the given instruction to point to the next block.
            /// </summary>
            public void PointToNext(int inIndex)
            {
                m_NextPointer = inIndex;
            }

            /// <summary>
            /// Sets the given instruction to point to the end of the block.
            /// </summary>
            public void PointToEnd(int inIndex)
            {
                m_EndPointers.Add(inIndex);
            }

            /// <summary>
            /// Ends the block.
            /// </summary>
            public void End(int inIndex, List<LeafInstruction> ioInstructions)
            {
                if (m_NextPointer != -1)
                {
                    Advance(inIndex, ioInstructions);
                }

                for(int i = m_EndPointers.Count - 1; i >= 0; --i)
                {
                    int idx = m_EndPointers[i];
                    LeafInstruction inst = ioInstructions[idx];
                    int jump = inIndex - idx;
                    inst.SetArg(jump);
                    ioInstructions[idx] = inst;
                }

                m_EndPointers.Clear();
            }

            /// <summary>
            /// Clears the block.
            /// </summary>
            public void Clear()
            {
                m_NextPointer = -1;
                m_EndPointers.Clear();
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

        private readonly ConditionalBlockLinker[] m_LinkerStack = new ConditionalBlockLinker[4];
        private int m_LinkerCount = 0;

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

            if (data.Id == "loop")
            {
                ProcessOptionalCondition(inPosition, data, LeafOpcode.Loop);
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

            // choice
            if (data.Id == "choice")
            {
                ProcessChoice(inPosition, data);
                return true;
            }

            // if statements
            else if (data.Id == "if")
            {
                EmitExpressionCall(data.Data);
                EmitInstruction(LeafOpcode.JumpIfFalse, -1); // todo: link to else or endif
            }
            else if (data.Id == "elseif")
            {
                EmitInstruction(LeafOpcode.Jump, -1); // todo: link to end of if block
            }
            else if (data.Id == "else")
            {
                EmitInstruction(LeafOpcode.Jump, -1); // todo: link to end of if block
            }
            else if (data.Id == "endif")
            {
                // todo: finish if block
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
            // Syntax
            // loop
            // loop expression
            // stop
            // stop expression
            // return
            // return expression

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

        #endregion // Emit

        #region Sequence Linker



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