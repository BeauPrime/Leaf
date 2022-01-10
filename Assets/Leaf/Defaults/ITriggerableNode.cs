/*
 * Copyright (C) 2022. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    3 Jan 2022
 * 
 * File:    ITriggerableNode.cs
 * Purpose: Interface for a node that can be triggered.
 */

using BeauUtil;
using BeauUtil.Variants;

namespace Leaf.Defaults
{
    public interface ITriggerableNode
    {
        StringHash32 TriggerId { get; }
        VariantComparison[] TriggerConditions { get; }
        TriggerMode TriggerMode { get; }
        int TriggerScore { get; }
    }

    public enum TriggerMode
    {
        Prioritized,
        Function
    }
}