/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    24 Oct 2020
 * 
 * File:    LeafRuntime.cs
 * Purpose: Execution environment for LeafThreadState objects.
 */

using System;
using System.Collections;
using BeauUtil;
using BeauUtil.Debugger;
using BeauUtil.Variants;
using UnityEngine;

namespace Leaf.Runtime
{
    /// <summary>
    /// Runtime environment for LeafThreadState.
    /// </summary>
    static public class LeafRuntime
    {
        /// <summary>
        /// Evaluates the given node, using the provided thread state.
        /// </summary>
        static public IEnumerator Execute<TNode>(ILeafPlugin<TNode> inPlugin, LeafThreadState<TNode> ioThreadState, TNode inNode)
            where TNode : LeafNode
        {
            if (inNode == null)
                return null;
            
            ioThreadState.PushNode(inNode);
            return Execute(inPlugin, ioThreadState);
        }

        /// <summary>
        /// Executes the given thread.
        /// </summary>
        static public IEnumerator Execute<TNode>(ILeafPlugin<TNode> inPlugin, LeafThreadState<TNode> ioThreadState)
            where TNode : LeafNode
        {
            TNode node;
            uint pc;
            LeafOpcode op;
            LeafInstructionBlock block;
            while (ioThreadState.HasNodes())
            {
                ioThreadState.ReadState(out node, out pc);

                block = node.Package().m_Instructions;

                // if we've exceeded our frame, pop out
                if (pc >= node.m_InstructionOffset + node.m_InstructionCount)
                {
                    ioThreadState.PopNode();
                    continue;
                }

                op = LeafInstruction.ReadOpcode(block.InstructionStream, ref pc);

                switch (op)
                {
                    // TEXT

                    case LeafOpcode.RunLine:
                        {
                            StringHash32 lineCode = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                            ioThreadState.WriteProgramCounter(pc);

                            string line;
                            if (LeafUtils.TryLookupLine(inPlugin, lineCode, node, out line))
                            {
                                IEnumerator process = inPlugin.RunLine(ioThreadState, line);
                                if (process != null)
                                    yield return process;
                            }
                            else
                            {
                                Log.Error("[LeafRuntime] Could not locate line '{0}' from node '{1}'", lineCode, node.Id());
                            }
                            break;
                        }

                    // EXPRESSIONS

                    case LeafOpcode.EvaluateSingleExpression:
                        {
                            uint expressionIdx = LeafInstruction.ReadUInt32(block.InstructionStream, ref pc);
                            ioThreadState.WriteProgramCounter(pc);

                            Variant result = EvaluateValueExpression(inPlugin, ref block.ExpressionTable[expressionIdx], ioThreadState, block.StringTable);
                            ioThreadState.PushValue(result);
                            break;
                        }

                    case LeafOpcode.EvaluateExpressionsAnd:
                        {
                            uint expressionOffset = LeafInstruction.ReadUInt32(block.InstructionStream, ref pc);
                            ushort expressionCount = LeafInstruction.ReadUInt16(block.InstructionStream, ref pc);
                            ioThreadState.WriteProgramCounter(pc);

                            bool bLocal;
                            bool bResult = true;
                            for(ushort i = 0; i < expressionCount; i++)
                            {
                                bLocal = EvaluateLogicalExpression(inPlugin, ref block.ExpressionTable[expressionOffset + i], ioThreadState, block.StringTable);
                                if (!bLocal)
                                {
                                    bResult = false;
                                    break;
                                }
                            }

                            ioThreadState.PushValue(bResult);
                            break;
                        }

                    case LeafOpcode.EvaluateExpressionsOr:
                        {
                            uint expressionOffset = LeafInstruction.ReadUInt32(block.InstructionStream, ref pc);
                            ushort expressionCount = LeafInstruction.ReadUInt16(block.InstructionStream, ref pc);
                            ioThreadState.WriteProgramCounter(pc);

                            bool bLocal;
                            bool bResult = false;
                            for(ushort i = 0; i < expressionCount; i++)
                            {
                                bLocal = EvaluateLogicalExpression(inPlugin, ref block.ExpressionTable[expressionOffset + i], ioThreadState, block.StringTable);
                                if (bLocal)
                                {
                                    bResult = true;
                                    break;
                                }
                            }

                            ioThreadState.PushValue(bResult);
                            break;
                        }

                    case LeafOpcode.EvaluateExpressionsGroup:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            // TODO: Implement
                            break;
                        }
                    
                    // INVOCATIONS

                    case LeafOpcode.Invoke:
                        {
                            MethodCall invocation;
                            invocation.Id = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                            invocation.Args = LeafInstruction.ReadStringTableString(block.InstructionStream, ref pc, block.StringTable);
                            ioThreadState.WriteProgramCounter(pc);

                            IEnumerator process = Invoke(inPlugin, invocation, ioThreadState, null);
                            if (process != null)
                                yield return process;
                            break;
                        }

                    case LeafOpcode.InvokeWithReturn:
                        {
                            MethodCall invocation;
                            invocation.Id = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                            invocation.Args = LeafInstruction.ReadStringTableString(block.InstructionStream, ref pc, block.StringTable);
                            ioThreadState.WriteProgramCounter(pc);

                            Variant result = InvokeWithReturn(inPlugin, invocation, ioThreadState, null);
                            ioThreadState.PushValue(result);
                            break;
                        }

                    case LeafOpcode.InvokeWithTarget:
                        {
                            MethodCall invocation;
                            invocation.Id = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                            invocation.Args = LeafInstruction.ReadStringTableString(block.InstructionStream, ref pc, block.StringTable);
                            ioThreadState.WriteProgramCounter(pc);

                            StringHash32 objectId = ioThreadState.PopValue().AsStringHash();
                            object target;

                            if (!LeafUtils.TryLookupObject(inPlugin, objectId, ioThreadState, out target))
                            {
                                Log.Warn("[LeafRuntime] Could not locate target {0} from node '{1}'", objectId, node.Id());
                                break;
                            }

                            IEnumerator process = Invoke(inPlugin, invocation, ioThreadState, target);
                            if (process != null)
                                yield return process;
                            break;
                        }
                    
                    // STACK

                    case LeafOpcode.PushValue:
                        {
                            Variant value = LeafInstruction.ReadVariant(block.InstructionStream, ref pc);
                            ioThreadState.WriteProgramCounter(pc);

                            ioThreadState.PushValue(value);
                            break;
                        }

                    case LeafOpcode.PopValue:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            ioThreadState.PopValue();
                            break;
                        }

                    case LeafOpcode.DuplicateValue:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            Variant current = ioThreadState.PeekValue();
                            ioThreadState.PushValue(current);
                            break;
                        }

                    // MEMORY

                    case LeafOpcode.LoadTableValue:
                        {
                            TableKeyPair keyPair = LeafInstruction.ReadTableKeyPair(block.InstructionStream, ref pc);
                            ioThreadState.WriteProgramCounter(pc);

                            Variant value = ioThreadState.GetVariable(keyPair, ioThreadState); 
                            ioThreadState.PushValue(value);
                            break;
                        }

                    case LeafOpcode.StoreTableValue:
                        {
                            TableKeyPair keyPair = LeafInstruction.ReadTableKeyPair(block.InstructionStream, ref pc);
                            ioThreadState.WriteProgramCounter(pc);

                            Variant value = ioThreadState.PopValue(); 
                            ioThreadState.SetVariable(keyPair, value, ioThreadState);
                            break;
                        }

                    case LeafOpcode.IncrementTableValue:
                        {
                            TableKeyPair keyPair = LeafInstruction.ReadTableKeyPair(block.InstructionStream, ref pc);
                            ioThreadState.WriteProgramCounter(pc);

                            ioThreadState.IncrementVariable(keyPair, 1, ioThreadState);
                            break;
                        }

                    // ARITHMETIC

                    case LeafOpcode.Add:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            Variant a = ioThreadState.PopValue();
                            Variant b = ioThreadState.PopValue();
                            Variant c = a + b;
                            ioThreadState.PushValue(c);

                            break;
                        }

                    case LeafOpcode.Subtract:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            Variant a = ioThreadState.PopValue();
                            Variant b = ioThreadState.PopValue();
                            Variant c = b - a;
                            ioThreadState.PushValue(c);

                            break;
                        }

                    case LeafOpcode.Multiply:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            Variant a = ioThreadState.PopValue();
                            Variant b = ioThreadState.PopValue();
                            Variant c = a * b;
                            ioThreadState.PushValue(c);

                            break;
                        }

                    case LeafOpcode.Divide:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            Variant a = ioThreadState.PopValue();
                            Variant b = ioThreadState.PopValue();
                            Variant c = b / a;
                            ioThreadState.PushValue(c);

                            break;
                        }

                    // LOGICAL OPERATORS

                    case LeafOpcode.Not:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            Variant a = ioThreadState.PopValue();
                            Variant b = !a.AsBool();
                            ioThreadState.PushValue(b);
                            break;
                        }

                    case LeafOpcode.CastToBool:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            Variant a = ioThreadState.PopValue().AsBool();
                            ioThreadState.PushValue(a);
                            break;
                        }

                    case LeafOpcode.LessThan:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            Variant a = ioThreadState.PopValue();
                            Variant b = ioThreadState.PopValue();
                            Variant c = b < a;
                            ioThreadState.PushValue(c);
                            break;
                        }

                    case LeafOpcode.LessThanOrEqualTo:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            Variant a = ioThreadState.PopValue();
                            Variant b = ioThreadState.PopValue();
                            Variant c = b <= a;
                            ioThreadState.PushValue(c);
                            break;
                        }

                    case LeafOpcode.EqualTo:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            Variant a = ioThreadState.PopValue();
                            Variant b = ioThreadState.PopValue();
                            Variant c = b == a;
                            ioThreadState.PushValue(c);
                            break;
                        }

                    case LeafOpcode.NotEqualTo:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            Variant a = ioThreadState.PopValue();
                            Variant b = ioThreadState.PopValue();
                            Variant c = b != a;
                            ioThreadState.PushValue(c);
                            break;
                        }

                    case LeafOpcode.GreaterThanOrEqualTo:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            Variant a = ioThreadState.PopValue();
                            Variant b = ioThreadState.PopValue();
                            Variant c = b >= a;
                            ioThreadState.PushValue(c);
                            break;
                        }

                    case LeafOpcode.GreaterThan:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            Variant a = ioThreadState.PopValue();
                            Variant b = ioThreadState.PopValue();
                            Variant c = b > a;
                            ioThreadState.PushValue(c);
                            break;
                        }
                    
                    // JUMPS

                    case LeafOpcode.Jump:
                        {
                            short relative = LeafInstruction.ReadInt16(block.InstructionStream, ref pc);
                            ioThreadState.WriteProgramCounter(pc);

                            ioThreadState.JumpRelative(relative);
                            break;
                        }

                    case LeafOpcode.JumpIfFalse:
                        {
                            short relative = LeafInstruction.ReadInt16(block.InstructionStream, ref pc);
                            ioThreadState.WriteProgramCounter(pc);

                            bool bValue = ioThreadState.PopValue().AsBool();
                            if (!bValue)
                            {
                                ioThreadState.JumpRelative(relative);
                            }
                            break;
                        }

                    case LeafOpcode.JumpIndirect:
                        {
                            ioThreadState.WriteProgramCounter(pc);
                            
                            int jump = ioThreadState.PopValue().AsInt();
                            ioThreadState.JumpRelative(jump);
                            break;
                        }

                    // FLOW CONTROL

                    case LeafOpcode.GotoNode:
                        {
                            StringHash32 nodeId = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                            ioThreadState.WriteProgramCounter(pc);

                            TryGotoNode(inPlugin, ioThreadState, node, nodeId);
                            break;
                        }

                    case LeafOpcode.GotoNodeIndirect:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            StringHash32 nodeId = ioThreadState.PopValue().AsStringHash();
                            TryGotoNode(inPlugin, ioThreadState, node, nodeId);
                            break;
                        }

                    case LeafOpcode.BranchNode:
                        {
                            StringHash32 nodeId = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                            ioThreadState.WriteProgramCounter(pc);

                            TryBranchNode(inPlugin, ioThreadState, node, nodeId);
                            break;
                        }

                    case LeafOpcode.BranchNodeIndirect:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            StringHash32 nodeId = ioThreadState.PopValue().AsStringHash();
                            TryBranchNode(inPlugin, ioThreadState, node, nodeId);
                            break;
                        }

                    case LeafOpcode.ReturnFromNode:
                        {
                            ioThreadState.PopNode();
                            break;
                        }

                    case LeafOpcode.Stop:
                        {
                            ioThreadState.ClearNodes();
                            break;
                        }

                    case LeafOpcode.Loop:
                        {
                            ioThreadState.ResetProgramCounter();
                            break;
                        }

                    case LeafOpcode.Yield:
                        {
                            ioThreadState.WriteProgramCounter(pc);
                            yield return null;
                            break;
                        }

                    // FORKING

                    case LeafOpcode.ForkNode:
                        {
                            StringHash32 nodeId = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                            ioThreadState.WriteProgramCounter(pc);

                            TryForkNode(inPlugin, ioThreadState, node, nodeId, true);
                            break;
                        }

                    case LeafOpcode.ForkNodeIndirect:
                        {
                            ioThreadState.WriteProgramCounter(pc);
                            
                            StringHash32 nodeId = ioThreadState.PopValue().AsStringHash();
                            TryForkNode(inPlugin, ioThreadState, node, nodeId, true);
                            break;
                        }

                    case LeafOpcode.ForkNodeUntracked:
                        {
                            StringHash32 nodeId = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                            ioThreadState.WriteProgramCounter(pc);

                            TryForkNode(inPlugin, ioThreadState, node, nodeId, false);
                            break;
                        }

                    case LeafOpcode.ForkNodeIndirectUntracked:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            StringHash32 nodeId = ioThreadState.PopValue().AsStringHash();
                            TryForkNode(inPlugin, ioThreadState, node, nodeId, false);
                            break;
                        }

                    case LeafOpcode.JoinForks:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            // TODO: Maybe a better way to wait?
                            while(ioThreadState.HasChildren())
                                yield return null;
                            break;
                        }

                    // CHOICES

                    case LeafOpcode.AddChoiceOption:
                        {
                            StringHash32 textId = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                            LeafChoice.OptionFlags flags = (LeafChoice.OptionFlags) LeafInstruction.ReadByte(block.InstructionStream, ref pc);
                            ioThreadState.WriteProgramCounter(pc);

                            bool bAvailable = ioThreadState.PopValue().AsBool();
                            Variant nodeId = ioThreadState.PopValue();

                            if (bAvailable)
                            {
                                flags |= LeafChoice.OptionFlags.IsAvailable;
                            }
                            ioThreadState.AddOption(nodeId, textId, flags);
                            break;
                        }

                    case LeafOpcode.AddChoiceAnswer:
                        {
                            StringHash32 answerId = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                            ioThreadState.WriteProgramCounter(pc);

                            Variant nodeId = ioThreadState.PopValue();
                            ioThreadState.AddOptionAnswer(answerId, nodeId);
                            break;
                        }

                    case LeafOpcode.ShowChoices:
                        {
                            ioThreadState.WriteProgramCounter(pc);

                            LeafChoice currentChoice = ioThreadState.GetOptions();
                            if (currentChoice.AvailableCount > 0)
                            {
                                yield return inPlugin.ShowOptions(ioThreadState, currentChoice);
                                Variant chosenNode = currentChoice.ChosenTarget();
                                currentChoice.Reset();
                                ioThreadState.PushValue(chosenNode);
                            }
                            else
                            {
                                currentChoice.Reset();
                                ioThreadState.PushValue(Variant.Null);
                            }
                            break;
                        }
                
                    case LeafOpcode.NoOp:
                        {
                            break;
                        }

                    default:
                        {
                            throw new InvalidOperationException("Unrecognized opcode " + op);
                        }
                }
            }

            inPlugin.OnEnd(ioThreadState);
        }

        #region Small Operations
    
        /// <summary>
        /// Attempts to switch the current thread to the given node.
        /// </summary>
        static public void TryGotoNode<TNode>(ILeafPlugin<TNode> inPlugin, LeafThreadState<TNode> ioThreadState, TNode inLocalNode, StringHash32 inNodeId)
            where TNode : LeafNode
        {
            if (inNodeId.IsEmpty)
            {
                ioThreadState.GotoNode(null);
                return;
            }

            TNode targetNode;
            if (LeafUtils.TryLookupNode(inPlugin, inNodeId, inLocalNode, out targetNode))
            {
                ioThreadState.GotoNode(targetNode);
            }
            else
            {
                Log.Error("[LeafRuntime] Could not go to node '{0}' from '{1}' - node not found",
                    inNodeId, inLocalNode.Id());
            }
        }

        /// <summary>
        /// Attempts to branch the current thread to the given node.
        /// Once the given node is finished, execution will resume at the previously loaded node.
        /// </summary>
        static public void TryBranchNode<TNode>(ILeafPlugin<TNode> inPlugin, LeafThreadState<TNode> ioThreadState, TNode inLocalNode, StringHash32 inNodeId)
            where TNode : LeafNode
        {
            if (inNodeId.IsEmpty)
            {
                return;
            }

            TNode targetNode;
            if (LeafUtils.TryLookupNode(inPlugin, inNodeId, inLocalNode, out targetNode))
            {
                ioThreadState.PushNode(targetNode);
            }
            else
            {
                Log.Error("[LeafRuntime] Could not branch to node '{0}' from '{1}' - node not found",
                    inNodeId, inLocalNode.Id());
            }
        }

        /// <summary>
        /// Attempts to fork a thread for the given node.
        /// </summary>
        static public void TryForkNode<TNode>(ILeafPlugin<TNode> inPlugin, LeafThreadState<TNode> ioThreadState, TNode inLocalNode, StringHash32 inNodeId, bool inbTrack)
            where TNode : LeafNode
        {
            if (inNodeId.IsEmpty)
            {
                return;
            }

            TNode targetNode;
            if (LeafUtils.TryLookupNode(inPlugin, inNodeId, inLocalNode, out targetNode))
            {
                var newThread = inPlugin.Fork(ioThreadState, targetNode);
                if (inbTrack && newThread != null)
                {
                    ioThreadState.AddChild(newThread);
                }
            }
            else
            {
                Log.Error("[LeafRuntime] Could not branch to node '{0}' from '{1}' - node not found",
                    inNodeId, inLocalNode.Id());
            }
        }

        #endregion // Small Operations

        #region Invocation

        /// <summary>
        /// Invokes the given method call from the given thread.
        /// </summary>
        static public IEnumerator Invoke(ILeafPlugin inPlugin, MethodCall inInvocation, LeafThreadState inThreadState, object inTarget)
        {
            IMethodCache cache = inPlugin.MethodCache;
            if (cache == null)
                throw new InvalidOperationException("Cannot use DefaultLeafInvocation if ILeafPlugin.MethodCache is not specified for plugin");
            
            bool bSuccess;
            object result;
            if (inTarget == null)
            {
                bSuccess = cache.TryStaticInvoke(inInvocation.Id, inInvocation.Args, inThreadState, out result);
            }
            else
            {
                bSuccess = cache.TryInvoke(inTarget, inInvocation.Id, inInvocation.Args, inThreadState, out result);
            }

            if (!bSuccess)
                Log.Error("[DefaultLeafInvocation] Unable to execute method '{0}'", inInvocation);

            return result as IEnumerator;
        }

        /// <summary>
        /// Invokes the given method call from the given thread.
        /// </summary>
        static public Variant InvokeWithReturn(ILeafPlugin inPlugin, MethodCall inInvocation, LeafThreadState inThreadState, object inTarget)
        {
            IMethodCache cache = inPlugin.MethodCache;
            if (cache == null)
                throw new InvalidOperationException("ILeafPlugin.MethodCache is not specified for plugin");
            
            bool bSuccess;
            object result;
            if (inTarget == null)
            {
                bSuccess = cache.TryStaticInvoke(inInvocation.Id, inInvocation.Args, inThreadState, out result);
            }
            else
            {
                bSuccess = cache.TryInvoke(inTarget, inInvocation.Id, inInvocation.Args, inThreadState, out result);
            }

            if (!bSuccess)
            {
                Log.Error("[LeafRuntime] Unable to execute method '{0}'", inInvocation);
                return default(Variant);
            }
            else
            {
                Variant variantResult;
                bSuccess = Variant.TryConvertFrom(result, out variantResult);
                if (!bSuccess)
                {
                    Log.Error("[LeafRuntime] Unable to convert result of method call '{0}' (type '{1}') to variant", inInvocation, result.GetType().Name);
                    return default(Variant);
                }

                return variantResult;
            }
        }

        #endregion // Invocation
    
        #region Expressions

        static internal Variant EvaluateValueExpression(ILeafPlugin inPlugin, ref LeafExpression inExpression, LeafThreadState ioThreadState, string[] inStringTable)
        {
            if ((inExpression.Flags & LeafExpression.TypeFlags.IsLogical) != 0)
            {
                return EvaluateLogicalExpression(inPlugin, ref inExpression, ioThreadState, inStringTable);
            }

            ref LeafExpression.Operand operand = ref inExpression.Left;
            Variant value;
            TryEvaluateOperand(inPlugin, ref operand, ioThreadState, inStringTable, out value);
            return value;
        }

        static internal bool TryEvaluateOperand(ILeafPlugin inPlugin, ref LeafExpression.Operand inOperand, LeafThreadState ioThreadState, string[] inStringTable, out Variant outValue)
        {
            switch(inOperand.Type)
            {
                case LeafExpression.OperandType.Value:
                    {
                        outValue = inOperand.Data.Value;
                        return true;
                    }
                case LeafExpression.OperandType.Read:
                    {
                        return ioThreadState.TryGetVariable(inOperand.Data.TableKey, ioThreadState, out outValue);
                    }
                case LeafExpression.OperandType.Method:
                    {
                        MethodCall call;
                        call.Id = inOperand.Data.MethodId;

                        uint stringIdx = inOperand.Data.MethodArgsIndex;
                        if (stringIdx == LeafInstruction.EmptyIndex)
                        {
                            call.Args = null;
                        }
                        else
                        {
                            call.Args = inStringTable[stringIdx];
                        }

                        outValue = InvokeWithReturn(inPlugin, call, ioThreadState, null);
                        return true;
                    }
                default:
                    {
                        throw new InvalidOperationException("Unknown expression operand type " + inOperand.Type);
                    }
            }
        }

        static internal bool EvaluateLogicalExpression(ILeafPlugin inPlugin, ref LeafExpression inExpression, LeafThreadState ioThreadState, string[] inStringTable)
        {
            bool leftExists;
            Variant left, right;
            leftExists = TryEvaluateOperand(inPlugin, ref inExpression.Left, ioThreadState, inStringTable, out left);

            switch(inExpression.Operator)
            {
                case VariantCompareOperator.Exists:
                    return leftExists;
                case VariantCompareOperator.DoesNotExist:
                    return !leftExists;
                case VariantCompareOperator.True:
                    return left.AsBool();
                case VariantCompareOperator.False:
                    return !left.AsBool();
            }

            TryEvaluateOperand(inPlugin, ref inExpression.Right, ioThreadState, inStringTable, out right);

            switch(inExpression.Operator)
            {
                case VariantCompareOperator.LessThan:
                    return left < right;
                case VariantCompareOperator.LessThanOrEqualTo:
                    return left <= right;
                case VariantCompareOperator.EqualTo:
                    return left == right;
                case VariantCompareOperator.NotEqualTo:
                    return left != right;
                case VariantCompareOperator.GreaterThanOrEqualTo:
                    return left >= right;
                case VariantCompareOperator.GreaterThan:
                    return left > right;

                default:
                    throw new InvalidOperationException("Unknown expression comparison operator" + inExpression.Operator);
            }
        }

        #endregion // Expressions
    }
}