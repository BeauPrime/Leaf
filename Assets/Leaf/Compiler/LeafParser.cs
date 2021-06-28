/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    25 Oct 2020
 * 
 * File:    LeafParser.cs
 * Purpose: Leaf block parser.
 */

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
    public abstract class LeafParser<TNode, TPackage> : AbstractBlockGenerator<TNode, TPackage>, ILeafCompilerPlugin<TNode>
        where TNode : LeafNode
        where TPackage : LeafNodePackage<TNode>
    {
        #region Compilers

        private readonly RingBuffer<LeafCompiler<TNode>> m_AvailableCompilers = new RingBuffer<LeafCompiler<TNode>>();

        private LeafCompiler<TNode> AllocCompiler()
        {
            if (m_AvailableCompilers.Count <= 0)
                return new LeafCompiler<TNode>(this);

            return m_AvailableCompilers.PopBack();
        }

        private void FreeCompiler(LeafCompiler<TNode> inCompiler)
        {
            m_AvailableCompilers.PushBack(inCompiler);
        }

        #endregion // Compilers

        #region Package

        public override void OnStart(IBlockParserUtil inUtil, TPackage inPackage)
        {
            inPackage.m_Compiler = AllocCompiler();
            inPackage.m_Compiler.StartModule(inPackage, IsVerbose);
            inPackage.Clear();
        }

        public override void OnEnd(IBlockParserUtil inUtil, TPackage inPackage, bool inbError)
        {
            var compiler = inPackage.m_Compiler;
            compiler.FinishModule(inPackage);
            inPackage.m_Compiler = null;

            FreeCompiler(compiler);
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
            inPackage.AddNode(node);
            
            outBlock = node;
            return true;
        }

        public override bool TryAddContent(IBlockParserUtil inUtil, TPackage inPackage, TNode inBlock, StringSlice inContent)
        {
            inPackage.m_Compiler.Process(inUtil.Position, inContent);
            return true;
        }

        public override void CompleteHeader(IBlockParserUtil inUtil, TPackage inPackage, TNode inBlock, TagData inAdditionalData)
        {
            inPackage.m_Compiler.StartNodeContent(inUtil.Position);
        }

        public override void CompleteBlock(IBlockParserUtil inUtil, TPackage inPackage, TNode inBlock, TagData inAdditionalData, bool inbError)
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
        /// Compiles the given string into an expression.
        /// </summary>
        public virtual ILeafExpression<TNode> CompileExpression(StringSlice inExpression, LeafExpressionType inType)
        {
            return new DefaultLeafExpression<TNode>(inExpression, inType);
        }

        /// <summary>
        /// Compiles the given method and arguments into an invocation.
        /// </summary>
        public virtual ILeafInvocation<TNode> CompileInvocation(StringSlice inMethod, StringSlice inArguments)
        {
            return new DefaultLeafInvocation<TNode>(inMethod, inArguments);
        }

        /// <summary>
        /// Creates a node for the given id and package.
        /// </summary>
        protected abstract TNode CreateNode(string inFullId, StringSlice inExtraData, TPackage inPackage);

        #endregion // Abstract
    }
}