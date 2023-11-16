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
    public struct NodeTriggerInfo
    {
        public StringHash32 TriggerId;
        public int Score;
        public StringHash32 TargetId;
        public int Priority;
        public VariantComparison[] Conditions;
    }
}