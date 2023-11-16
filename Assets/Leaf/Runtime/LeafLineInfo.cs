/*
 * Copyright (C) 2023. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    14 Nov 2023
 * 
 * File:    LeafLineInfo.cs
 * Purpose: Line information passed into ILeafPlugin.RunLine
 */

using BeauUtil;

namespace Leaf
{
    /// <summary>
    /// Keyed line of text.
    /// </summary>
    public readonly struct LeafLineInfo
    {
        /// <summary>
        /// Line code.
        /// </summary>
        public readonly StringHash32 LineCode;

        /// <summary>
        /// Text for the line.
        /// </summary>
        public readonly StringSlice Text;

        public LeafLineInfo(StringHash32 inLineCode, StringSlice inText)
        {
            LineCode = inLineCode;
            Text = inText;
        }

        /// <summary>
        /// Returns if the text is empty or solely whitespace.
        /// </summary>
        public bool IsEmptyOrWhitespace
        {
            get { return Text.IsEmpty || Text.IsWhitespace; }
        }
    }
}