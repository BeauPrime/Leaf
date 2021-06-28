/*
 * Copyright (C) 2021. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    6 June 2021
 * 
 * File:    LeafUtils.cs
 * Purpose: Leaf utility methods.
 */

using System;
using System.Collections;
using System.Text;
using BeauRoutine;
using BeauUtil;
using BeauUtil.Variants;
using Leaf.Runtime;
using UnityEngine;

namespace Leaf
{
    /// <summary>
    /// Leaf utility methods
    /// </summary>
    static public class LeafUtils
    {
        /// <summary>
        /// Special "this" identifier.
        /// </summary>
        static public readonly StringHash32 ThisIdentifier = "this";

        /// <summary>
        /// Special "thread" identifier.
        /// </summary>
        static public readonly StringHash32 ThreadIdentifier = "thread";

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

        #region Method Cache

        /// <summary>
        /// Creates a new method cache for use by a leaf plugin.
        /// </summary>
        static public MethodCache<LeafMember> CreateMethodCache()
        {
            return new MethodCache<LeafMember>(typeof(MonoBehaviour), new LeafStringConverter());
        }

        /// <summary>
        /// Creates a new method cache for use by a leaf plugin.
        /// </summary>
        static public MethodCache<LeafMember> CreateMethodCache(Type inComponentType)
        {
            return new MethodCache<LeafMember>(inComponentType, new LeafStringConverter());
        }

        #endregion // Method Cache

        #region Default Leaf Members

        /// <summary>
        /// Waits for the given number of seconds.
        /// </summary>
        [LeafMember("Wait")]
        static public IEnumerator Wait(float inSeconds)
        {
            yield return inSeconds;
        }

        /// <summary>
        /// Waits for the given number of seconds.
        /// This is in real time and does not account for time scale.
        /// </summary>
        [LeafMember("WaitAbs")]
        static public IEnumerator WaitAbs(float inSeconds)
        {
            return Routine.WaitRealSeconds(inSeconds);
        }

        #endregion // Default Leaf Members
    }
}