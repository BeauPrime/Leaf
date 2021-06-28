/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    24 Oct 2020
 * 
 * File:    ILeafExpression.cs
 * Purpose: Interface for an evaluatable expression.
 */

using BeauUtil.Variants;

namespace Leaf.Runtime
{
    public interface ILeafExpression { }

    public interface ILeafExpression<TNode> : ILeafExpression
        where TNode : LeafNode
    {
        Variant Evaluate(LeafThreadState<TNode> inThreadState, ILeafPlugin<TNode> inPlugin);
        void Assign(LeafThreadState<TNode> inThreadState, ILeafPlugin<TNode> inPlugin);
    }

    /// <summary>
    /// Expression type.
    /// </summary>
    public enum LeafExpressionType : byte
    {
        Evaluate,
        Assign
    }
}