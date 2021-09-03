/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    24 Oct 2020
 * 
 * File:    ILeafPlugin.cs
 * Purpose: Plugin for operating leaf nodes.
 */

using System.Collections;
using BeauUtil;
using BeauUtil.Variants;
using Leaf.Runtime;

namespace Leaf
{
    public interface ILeafPlugin : ILeafVariableAccess
    {
        IMethodCache MethodCache { get; }
    }

    public interface ILeafPlugin<TNode> : ILeafPlugin
        where TNode : LeafNode
    {
        void OnNodeEnter(TNode inNode, LeafThreadState<TNode> inThreadState);
        void OnNodeExit(TNode inNode, LeafThreadState<TNode> inThreadState);

        void OnEnd(LeafThreadState<TNode> inThreadState);

        bool TryLookupLine(StringHash32 inLineCode, TNode inLocalNode, out string outLine);
        bool TryLookupNode(StringHash32 inNodeId, TNode inLocalNode, out TNode outNode);
        bool TryLookupObject(StringHash32 inObjectId, LeafThreadState<TNode> inThreadState, out object outObject);

        IEnumerator RunLine(LeafThreadState<TNode> inThreadState, StringSlice inLine, ILeafContentResolver inContentResolver);
        IEnumerator ShowOptions(LeafThreadState<TNode> inThreadState, LeafChoice inChoice, ILeafContentResolver inContentResolver);
        
        LeafThreadState<TNode> Fork(LeafThreadState<TNode> inParentThreadState, TNode inForkNode);
    }
}