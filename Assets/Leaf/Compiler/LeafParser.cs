/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    25 Oct 2020
 * 
 * File:    LeafParser.cs
 * Purpose: Leaf block parser.
 */

using System;
using System.Text;
using BeauUtil;
using BeauUtil.Blocks;
using BeauUtil.Tags;
using Leaf.Defaults;
using Leaf.Runtime;

namespace Leaf.Compiler
{
    /// <summary>
    /// Block parser for leaf nodes.
    /// </summary>
    public abstract class LeafParser<TNode, TPackage> : AbstractBlockGenerator<TNode, TPackage>, ILeafCompilerPlugin
        where TNode : LeafNode
        where TPackage : LeafNodePackage<TNode>
    {
        public IMethodCache MethodCache;

        #region Compilers

        private readonly RingBuffer<LeafCompiler> m_AvailableCompilers = new RingBuffer<LeafCompiler>();

        private LeafCompiler AllocCompiler()
        {
            if (m_AvailableCompilers.Count <= 0)
                return new LeafCompiler(this);

            return m_AvailableCompilers.PopBack();
        }

        private void FreeCompiler(LeafCompiler inCompiler)
        {
            m_AvailableCompilers.PushBack(inCompiler);
        }

        #endregion // Compilers

        #region Package

        public override void OnStart(IBlockParserUtil inUtil, TPackage inPackage)
        {
            inPackage.m_Compiler = AllocCompiler();
            inPackage.m_Compiler.StartModule(inPackage, MethodCache, inUtil, CompilerFlags);
            inPackage.Clear();
        }

        public override void OnEnd(IBlockParserUtil inUtil, TPackage inPackage, bool inbError)
        {
            var compiler = inPackage.m_Compiler;
            compiler.FinishModule(inPackage);
            inPackage.m_Compiler = null;

            FreeCompiler(compiler);
        }

        public override bool TryEvaluatePackage(IBlockParserUtil inUtil, TPackage inPackage, TNode inCurrentBlock, TagData inMetadata)
        {
            StringHash32 id = inMetadata.Id;
            if (id == LeafTokens.Macro)
            {
                GenerateMacro(inUtil, inPackage.m_Compiler, inMetadata.Data);
                return true;
            }

            if (id == LeafTokens.Const)
            {
                TagData constDefinition = TagData.Parse(inMetadata.Data, TagStringParser.CurlyBraceDelimiters);
                inPackage.m_Compiler.DefineConst(constDefinition.Id, constDefinition.Data);
                return true;
            }

            return false;
        }

        private void GenerateMacro(IBlockParserUtil inUtil, LeafCompiler inCompiler, StringSlice inData)
        {
            StringSlice firstLine = inData;
            int lineIndex = inData.IndexOf('\n');
            if (lineIndex >= 0)
            {
                firstLine = inData.Substring(0, lineIndex);
            }

            int openParenIdx = firstLine.IndexOf('(');
            int closeParenIdx = firstLine.LastIndexOf(')');

            if (openParenIdx < 0 || closeParenIdx < 0)
            {
                throw new SyntaxException(inUtil.Position, "Macro definition '{0}' does not have property formatted () operators for declaration", inData);
            }

            StringSlice replaceContents = inData.Substring(closeParenIdx + 1).TrimStart();

            StringSlice idSlice = firstLine.Substring(0, openParenIdx).TrimEnd();
            int argsLength = closeParenIdx - 1 - openParenIdx;
            StringSlice args = firstLine.Substring(openParenIdx + 1, argsLength);

            inCompiler.DefineMacro(idSlice, args, replaceContents);
        }

        public override void ProcessLine(IBlockParserUtil inUtil, TPackage inPackage, TNode inBlock, StringBuilder ioLine)
        {
            inPackage.m_Compiler.PreprocessLine(ioLine);
        }

        #endregion // Package

        #region Nodes

        public override bool TryCreateBlock(IBlockParserUtil inUtil, TPackage inPackage, TagData inId, out TNode outBlock)
        {
            inUtil.TempBuilder.Length = 0;

            StringSlice rootPath = inPackage.RootPath();
            string fullId = LeafUtils.AssembleFullId(inUtil.TempBuilder, rootPath, inId.Id, PathSeparator);
            inPackage.m_Compiler.StartNode(fullId, inUtil.Position);

            TNode node = CreateNode(fullId, inId.Data, inPackage);
            try
            {
                inPackage.AddNode(node);
            }
            catch
            {
                throw new SyntaxException(inUtil.Position, "Duplicate node ids {0}", fullId);
            }
            
            outBlock = node;
            return true;
        }

        public override bool TryAddContent(IBlockParserUtil inUtil, TPackage inPackage, TNode inBlock, StringBuilder inContent)
        {
            inPackage.m_Compiler.Process(inUtil.Position, inContent.ToString());
            return true;
        }

        public override void CompleteHeader(IBlockParserUtil inUtil, TPackage inPackage, TNode inBlock)
        {
            inPackage.m_Compiler.StartNodeContent(inUtil.Position);
        }

        public override void CompleteBlock(IBlockParserUtil inUtil, TPackage inPackage, TNode inBlock, bool inbError)
        {
            inPackage.m_Compiler.FinishNode(inBlock, inUtil.Position);
        }

        #endregion // Nodes
    
        #region Abstract

        /// <summary>
        /// Indicates if compilation will output verbose debugging information.
        /// </summary>
        public virtual bool IsVerbose
        {
            get { return UnityEngine.Application.isEditor && !UnityEngine.Application.isPlaying; }
        }

        /// <summary>
        /// Character separator for node paths.
        /// </summary>
        public virtual char PathSeparator
        {
            get { return '.'; }
        }

        /// <summary>
        /// Indicates if successive lines of content should be collapsed into a single line.
        /// </summary>
        public virtual bool CollapseContent
        {
            get { return false; }
        }

        /// <summary>
        /// Compilation flags.
        /// </summary>
        public virtual LeafCompilerFlags CompilerFlags
        {
            get
            {
                LeafCompilerFlags flags = 0;
                if (IsVerbose)
                    flags |= LeafCompilerFlags.Default_Development;
                if (CollapseContent)
                    flags |= LeafCompilerFlags.Parse_CollapseContent;
                return flags;
            }
        }

        /// <summary>
        /// Creates a node for the given id and package.
        /// </summary>
        protected abstract TNode CreateNode(string inFullId, StringSlice inExtraData, TPackage inPackage);

        #endregion // Abstract
    }
}