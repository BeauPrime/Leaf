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
using BeauUtil.Debugger;
using BeauUtil.Variants;
using Leaf.Compiler;
using Leaf.Runtime;

namespace Leaf.Defaults
{
    public class DefaultLeafExpression<TNode> : ILeafExpression<TNode>
        where TNode : LeafNode
    {
        private enum Mode : byte
        {
            Comparison,
            Modification,
            Operand,
            List
        }

        private readonly Mode m_Mode;
        private readonly VariantOperand m_SingleLeft;
        private readonly byte m_SingleOperator;
        private readonly VariantOperand m_SingleRight;
        private readonly StringSlice m_Expression;

        public DefaultLeafExpression(StringSlice inExpression, LeafExpressionType inType)
        {
            if (StringUtils.ArgsList.IsList(inExpression))
            {
                m_Mode = Mode.List;
                m_Expression = inExpression;
            }
            else if (inType == LeafExpressionType.Assign)
            {
                VariantModification modification;
                if (!VariantModification.TryParse(inExpression, out modification))
                {
                    throw new ArgumentException("inExpression");
                }

                m_Mode = Mode.Modification;
                m_SingleLeft = new VariantOperand(modification.VariableKey);
                m_SingleOperator = (byte) modification.Operator;
                m_SingleRight = modification.Operand;
            }
            else
            {
                VariantComparison comp;
                if (VariantOperand.TryParse(inExpression, out m_SingleLeft))
                {
                    m_Mode = Mode.Operand;
                }
                else if (VariantComparison.TryParse(inExpression, out comp))
                {
                    m_Mode = Mode.Comparison;
                    m_SingleLeft = comp.Left;
                    m_SingleOperator = (byte) comp.Operator;
                    m_SingleRight = comp.Right;
                }
                else
                {
                    throw new ArgumentException("inExpression");
                }
            }
        }

        public Variant Evaluate(LeafThreadState<TNode> inThreadState, ILeafPlugin<TNode> inPlugin)
        {
            IVariantResolver resolver = inThreadState.Resolver ?? inPlugin.Resolver;
            if (resolver == null)
                throw new InvalidOperationException("Cannot use DefaultLeafExpression if resolver is not specified for thread and DefaultVariantResolver is not specified for plugin");

            switch(m_Mode)
            {
                case Mode.Operand:
                    {
                        Variant value;
                        m_SingleLeft.TryResolve(resolver, inThreadState, out value, inPlugin.MethodCache);
                        return value;
                    }

                case Mode.Comparison:
                    {
                        VariantComparison comp;
                        comp.Left = m_SingleLeft;
                        comp.Operator = (VariantCompareOperator) m_SingleOperator;
                        comp.Right = m_SingleRight;
                        return comp.Evaluate(resolver, inThreadState, inPlugin.MethodCache);
                    }

                case Mode.List:
                    return resolver.TryEvaluate(inThreadState, m_Expression, inPlugin.MethodCache);

                default:
                    throw new InvalidOperationException("Invalid evaluate expression mode " + m_Mode.ToString());
            }
        }

        public void Assign(LeafThreadState<TNode> inThreadState, ILeafPlugin<TNode> inPlugin)
        {
            IVariantResolver resolver = inThreadState.Resolver ?? inPlugin.Resolver;
            if (resolver == null)
                throw new InvalidOperationException("Cannot use DefaultLeafExpression if resolver is not specified for thread and DefaultVariantResolver is not specified for plugin");

            switch(m_Mode)
            {
                case Mode.Modification:
                    {
                        VariantModification mod;
                        mod.VariableKey = m_SingleLeft.TableKey;
                        mod.Operator = (VariantModifyOperator) m_SingleOperator;
                        mod.Operand = m_SingleRight;

                        if (!mod.Execute(resolver, inThreadState, inPlugin.MethodCache))
                        {
                            Log.Error("[DefaultLeafExpression] Unable to execute modification {0}", mod);
                        }
                        break;
                    }

                case Mode.List:
                    {
                        if (!resolver.TryModify(inThreadState, m_Expression, inPlugin.MethodCache))
                        {
                            Log.Error("[DefaultLeafExpression] Unable to execute modification '{0}'", m_Expression);
                        }
                        break;
                    }

                default:
                    throw new InvalidOperationException("Invalid assign expression mode " + m_Mode.ToString());
            }
        }
    }
}