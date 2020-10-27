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
    public abstract class LeafNode : IDataBlock
    {
        protected StringHash32 m_Id;
        protected LeafInstruction[] m_Instructions;

        public StringHash32 Id() { return m_Id; }
        public LeafInstruction[] Instructions() { return m_Instructions; }
        public abstract ILeafModule Module();

        internal void SetInstructions(LeafInstruction[] inInstructions)
        {
            m_Instructions = inInstructions;
        }

        /// <summary>
        /// Returns if the given node identifier is valid.
        /// </summary>
        static public bool IsValidIdentifier(StringSlice inIdentifier)
        {
            return VariantUtils.IsValidIdentifier(inIdentifier);
        }

        static internal string AssembleFullId(StringBuilder ioBuilder, StringSlice inRoot, StringSlice inId, char inSeparator)
        {
            if (!inRoot.IsEmpty)
            {
                ioBuilder.AppendSlice(inRoot);
                if (!inRoot.EndsWith(inSeparator))
                {
                    ioBuilder.Append(inSeparator);
                }
                ioBuilder.AppendSlice(inId);
                return ioBuilder.Flush();
            }
            
            return inId.ToString();
        }
    }
}