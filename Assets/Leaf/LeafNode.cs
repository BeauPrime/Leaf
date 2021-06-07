/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    24 Oct 2020
 * 
 * File:    LeafNode.cs
 * Purpose: Abstract Leaf node.
 */

using System.Text;
using BeauUtil;
using BeauUtil.Blocks;
using BeauUtil.Variants;
using Leaf.Runtime;

namespace Leaf
{
    /// <summary>
    /// Leaf node.
    /// </summary>
    public class LeafNode : IDataBlock
    {
        protected StringHash32 m_Id;
        protected ILeafModule m_Module;
        protected LeafInstruction[] m_Instructions;

        public StringHash32 Id() { return m_Id; }
        public LeafInstruction[] Instructions() { return m_Instructions; }
        public ILeafModule Module() { return m_Module; }

        public LeafNode(StringHash32 inId, ILeafModule inModule)
        {
            m_Id = inId;
            m_Module = inModule;
        }

        internal void SetInstructions(LeafInstruction[] inInstructions)
        {
            m_Instructions = inInstructions;
        }
    }
}