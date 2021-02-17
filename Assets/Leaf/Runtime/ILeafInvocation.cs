/*
 * Copyright (C) 2021. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    16 Feb 2021
 * 
 * File:    ILeafInvocation.cs
 * Purpose: Interface for an invocable call.
 */

using System.Collections;
using BeauUtil;

namespace Leaf.Runtime
{
    public interface ILeafInvocation { }

    public interface ILeafInvocation<TNode> : ILeafInvocation
        where TNode : LeafNode
    {
        IEnumerator Invoke(LeafThreadState<TNode> inThreadState, ILeafPlugin<TNode> inPlugin, object inTarget);
    }
}