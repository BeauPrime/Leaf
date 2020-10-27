/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    24 Oct 2020
 * 
 * File:    LeafNodePackage.cs
 * Purpose: Package of leaf nodes.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using BeauUtil;
using BeauUtil.Blocks;
using Leaf.Compiler;
using Leaf.Runtime;

namespace Leaf
{
    public abstract class LeafNodePackage<TNode> : IDataBlockPackage<TNode>, ILeafModule
        where TNode : LeafNode
    {
        // vars

        protected string m_Name;
        [BlockMeta("basePath")] protected string m_RootPath = string.Empty;

        // temp storage
        internal LeafCompiler<TNode> m_Compiler;

        // storage

        protected readonly Dictionary<StringHash32, TNode> m_Nodes = new Dictionary<StringHash32, TNode>(32);
        protected readonly Dictionary<StringHash32, string> m_LineTable = new Dictionary<StringHash32, string>(32);
        protected ILeafExpression<TNode>[] m_ExpressionTable = Array.Empty<ILeafExpression<TNode>>();

        protected LeafNodePackage(string inName)
        {
            m_Name = inName;
        }

        public string Name() { return m_Name; }
        public string RootPath() { return m_RootPath; }

        public int Count { get { return m_Nodes.Count; } }

        #region Modifications

        internal void AddNode(TNode inNode)
        {
            m_Nodes.Add(inNode.Id(), inNode);
        }

        internal void SetLines(Dictionary<StringHash32, string> inLines)
        {
            m_LineTable.Clear();
            foreach(var line in inLines)
            {
                m_LineTable.Add(line.Key, line.Value);
            }
        }

        internal void SetExpressions(ILeafExpression<TNode>[] inExpressions)
        {
            m_ExpressionTable = inExpressions;
        }

        #endregion // Modifications
        
        #region ILeafModule

        /// <summary>
        /// Attempts to retrieve the node with the specific id in this package.
        /// </summary>
        public bool TryGetNode(StringHash32 inNodeId, out TNode outNode)
        {
            return m_Nodes.TryGetValue(inNodeId, out outNode);
        }

        /// <summary>
        /// Attempts to retrieve the line with the specific line code in this package.
        /// </summary>
        public bool TryGetLine(StringHash32 inLineCode, out string outLine)
        {
            return m_LineTable.TryGetValue(inLineCode, out outLine);
        }

        bool ILeafContentResolver.TryGetNode(StringHash32 inNodeId, LeafNode inLocalNode, out LeafNode outNode)
        {
            TNode node;
            bool bResult = TryGetNode(inNodeId, out node);
            outNode = node;
            return bResult;
        }

        bool ILeafContentResolver.TryGetLine(StringHash32 inLineCode, LeafNode inLocalNode, out string outLine)
        {
            return TryGetLine(inLineCode, out outLine);
        }

        bool ILeafModule.TryGetExpression(uint inExpressionCode, out ILeafExpression outExpression)
        {
            if (inExpressionCode >= m_ExpressionTable.Length)
            {
                outExpression = null;
                return false;
            }

            outExpression = m_ExpressionTable[(int) inExpressionCode];
            return true;
        }

        /// <summary>
        /// Returns all lines embedded in this package.
        /// </summary>
        public IEnumerable<KeyValuePair<StringHash32, string>> AllLines()
        {
            return m_LineTable;
        }

        #endregion // ILeafModule

        #region IEnumerable

        public IEnumerator<TNode> GetEnumerator()
        {
            return m_Nodes.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion // IEnumerable

        /// <summary>
        /// Clears this package.
        /// </summary>
        public virtual void Clear()
        {
            m_Nodes.Clear();
            m_LineTable.Clear();
            m_ExpressionTable = Array.Empty<ILeafExpression<TNode>>();
        }
    }
}