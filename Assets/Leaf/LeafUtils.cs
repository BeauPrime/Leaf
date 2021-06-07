/*
 * Copyright (C) 2021. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    6 June 2021
 * 
 * File:    LeafUtils.cs
 * Purpose: Leaf utility methods.
 */

using System.Text;
using BeauUtil;
using BeauUtil.Blocks;
using BeauUtil.Tags;
using BeauUtil.Variants;
using Leaf.Runtime;

namespace Leaf
{
    /// <summary>
    /// Leaf utility methods
    /// </summary>
    static public class LeafUtils
    {
        #region Identifiers

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

        #endregion // Identifiers
    }
}