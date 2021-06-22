/*
 * Copyright (C) 2021. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    12 May 2021
 * 
 * File:    DefaultLeafExpression.cs
 * Purpose: Default leaf expression implementation.
 */

using System;
using BeauUtil;
using BeauUtil.Variants;
using Leaf.Runtime;

namespace Leaf.Defaults
{
    public class DefaultLeafExpression<TNode> : ILeafExpression<TNode>
        where TNode : LeafNode
    {
        private readonly StringSlice m_Expression;

        public DefaultLeafExpression(StringSlice inExpression)
        {
            m_Expression = inExpression;
        }

        public Variant Evaluate(LeafThreadState<TNode> inThreadState, ILeafPlugin<TNode> inPlugin)
        {
            IVariantResolver resolver = inThreadState.Resolver ?? inPlugin.Resolver;
            if (resolver == null)
                throw new InvalidOperationException("Cannot use DefaultLeafExpression if resolver is not specified for thread and DefaultVariantResolver is not specified for plugin");

            TableKeyPair keyPair;
            if (TableKeyPair.TryParse(m_Expression, out keyPair))
            {
                Variant value;
                resolver.TryResolve(inThreadState, keyPair, out value);
                return value;
            }

            MethodCall call;
            if (MethodCall.TryParse(m_Expression, out call))
            {
                Variant value;
                Variant.TryConvertFrom(inPlugin.MethodCache.StaticInvoke(call), out value);
                return value;
            }
            
            return resolver.TryEvaluate(inThreadState, m_Expression, inPlugin.MethodCache);
        }

        public void Set(LeafThreadState<TNode> inThreadState, ILeafPlugin<TNode> inPlugin)
        {
            IVariantResolver resolver = inThreadState.Resolver ?? inPlugin.Resolver;
            if (resolver == null)
                throw new InvalidOperationException("Cannot use DefaultLeafExpression if resolver is not specified for thread and DefaultVariantResolver is not specified for plugin");

            if (!resolver.TryModify(inThreadState, m_Expression, inPlugin.MethodCache))
            {
                UnityEngine.Debug.LogErrorFormat("[DefaultLeafExpression] Failed to set variables from string '{0}'", m_Expression);
            }
        }
    }
}