/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    26 Oct 2020
 * 
 * File:    ILeafContentResolver.cs
 * Purpose: Interface for resolving content.
 */

using BeauUtil;

namespace Leaf
{
    public interface ILeafContentResolver
    {
        bool TryGetNode(StringHash32 inNodeId, LeafNode inLocalNode, out LeafNode outNode);
        bool TryGetLine(StringHash32 inLineCode, LeafNode inLocalNode, out string outLine);
    }
}