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
        static public IEnumerator Execute<TNode>(LeafThreadState<TNode> ioThreadState, TNode inNode)
            where TNode : LeafNode
        {
            if (inNode == null)
                return null;
            
            ioThreadState.PushNode(inNode);
            return Execute(ioThreadState);
        }

        /// <summary>
        /// Executes the given thread.
        /// </summary>
        static public IEnumerator Execute<TNode>(LeafThreadState<TNode> ioThreadState)
            where TNode : LeafNode
        {
            return ioThreadState.GetExecutor();
        }

        internal sealed class Executor<TNode> : IEnumerator, IDisposable
            where TNode : LeafNode
        {
            private const int State_Default = 0;
            private const int State_Choose = 1;
            private const int State_Join = 2;
            private const int State_Done = -1;

            public readonly ILeafPlugin<TNode> Plugin;
            public readonly LeafThreadState<TNode> Thread;
            public IEnumerator Wait;
            public int State;
            public LeafThreadState.RegisterState Registers;

            public Executor(ILeafPlugin<TNode> inPlugin, LeafThreadState<TNode> inThreadState)
            {
                Plugin = inPlugin;
                Thread = inThreadState;
            }

            public object Current { get { return Wait; } }

            public void Dispose() { }

            public bool MoveNext()
            {
                if (State == State_Done)
                {
                    return false;
                }

                TNode node;
                uint pc;
                LeafInstructionBlock block;
                LeafOpcode op;
                string line;
                MethodCall method;
                LeafChoice choice;
                Wait = null;

                switch(State)
                {
                    case State_Choose:
                        {
                            Registers.B0_Variant = Thread.GetChosenOption();
                            Thread.ResetOptions();
                            Thread.PushValue(Registers.B0_Variant);
                            State = State_Default;
                            break;
                        }
                    case State_Join:
                        {
                            // TODO: Better check??
                            if (Thread.HasChildren())
                            {
                                return true;
                            }

                            State = State_Default;
                            break;
                        }
                }
                State = State_Default;

                while(Thread.HasNodes())
                {
                    Thread.ReadState(out node, out pc);
                    block = node.Package().m_Instructions;

                    if (pc >= node.m_InstructionOffset + node.m_InstructionCount)
                    {
                        Thread.PopNode();
                        continue;
                    }

                    op = LeafInstruction.ReadOpcode(block.InstructionStream, ref pc);

                    switch (op)
                    {
                        // TEXT

                        case LeafOpcode.RunLine:
                            {
                                Registers.B1_Identifier  = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                                Thread.WriteProgramCounter(pc);

                                if (LeafUtils.TryLookupLine(Plugin, Registers.B1_Identifier, node, out line))
                                {
                                    Wait = Plugin.RunLine(Thread, line);
                                    if (Wait != null)
                                    {
                                        return true;
                                    }
                                }
                                else
                                {
                                    Log.Error("[LeafRuntime] Could not locate line '{0}' from node '{1}'", Registers.B1_Identifier, node.Id());
                                }
                                break;
                            }

                        // EXPRESSIONS

                        case LeafOpcode.EvaluateSingleExpression:
                            {
                                Registers.B0_Offset = LeafInstruction.ReadUInt32(block.InstructionStream, ref pc);
                                Thread.WriteProgramCounter(pc);

                                Registers.B0_Variant = EvaluateValueExpression(Plugin, ref block.ExpressionTable[Registers.B0_Offset], Thread, block.StringTable);
                                Thread.PushValue(Registers.B0_Variant);
                                break;
                            }

                        case LeafOpcode.EvaluateExpressionsAnd:
                            {
                                Registers.B0_Offset = LeafInstruction.ReadUInt32(block.InstructionStream, ref pc);
                                Registers.B0_Count = LeafInstruction.ReadUInt16(block.InstructionStream, ref pc);
                                Thread.WriteProgramCounter(pc);

                                bool bResult = true;
                                for(ushort i = 0; i < Registers.B0_Count; i++)
                                {
                                    if (!EvaluateLogicalExpression(Plugin, ref block.ExpressionTable[Registers.B0_Offset + i], Thread, block.StringTable))
                                    {
                                        bResult = false;
                                        break;
                                    }
                                }

                                Thread.PushValue(bResult);
                                break;
                            }

                        case LeafOpcode.EvaluateExpressionsOr:
                            {
                                Registers.B0_Offset = LeafInstruction.ReadUInt32(block.InstructionStream, ref pc);
                                Registers.B0_Count = LeafInstruction.ReadUInt16(block.InstructionStream, ref pc);
                                Thread.WriteProgramCounter(pc);

                                bool bResult = false;
                                for(ushort i = 0; i < Registers.B0_Count; i++)
                                {
                                    if (EvaluateLogicalExpression(Plugin, ref block.ExpressionTable[Registers.B0_Offset + i], Thread, block.StringTable))
                                    {
                                        bResult = true;
                                        break;
                                    }
                                }

                                Thread.PushValue(bResult);
                                break;
                            }

                        case LeafOpcode.EvaluateExpressionsGroup:
                            {
                                Thread.WriteProgramCounter(pc);

                                // TODO: Implement
                                break;
                            }
                        
                        // INVOCATIONS

                        case LeafOpcode.Invoke:
                            {
                                method.Id = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                                method.Args = LeafInstruction.ReadStringTableString(block.InstructionStream, ref pc, block.StringTable);
                                Thread.WriteProgramCounter(pc);

                                Wait = Invoke(Plugin, method, Thread, null);
                                if (Wait != null)
                                {
                                    return true;
                                }
                                break;
                            }

                        case LeafOpcode.InvokeWithReturn:
                            {
                                method.Id = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                                method.Args = LeafInstruction.ReadStringTableString(block.InstructionStream, ref pc, block.StringTable);
                                Thread.WriteProgramCounter(pc);

                                Registers.B0_Variant = InvokeWithReturn(Plugin, method, Thread, null);
                                Thread.PushValue(Registers.B0_Variant);
                                break;
                            }

                        case LeafOpcode.InvokeWithTarget:
                            {
                                method.Id = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                                method.Args = LeafInstruction.ReadStringTableString(block.InstructionStream, ref pc, block.StringTable);
                                Thread.WriteProgramCounter(pc);

                                Registers.B1_Identifier = Thread.PopValue().AsStringHash();
                                object target;

                                if (!LeafUtils.TryLookupObject(Plugin, Registers.B1_Identifier, Thread, out target))
                                {
                                    Log.Warn("[LeafRuntime] Could not locate target {0} from node '{1}'", Registers.B1_Identifier, node.Id());
                                    break;
                                }

                                Wait = Invoke(Plugin, method, Thread, target);
                                if (Wait != null)
                                {
                                    return true;
                                }
                                break;
                            }
                        
                        // STACK

                        case LeafOpcode.PushValue:
                            {
                                Registers.B0_Variant = LeafInstruction.ReadVariant(block.InstructionStream, ref pc);
                                Thread.WriteProgramCounter(pc);

                                Thread.PushValue(Registers.B0_Variant);
                                break;
                            }

                        case LeafOpcode.PopValue:
                            {
                                Thread.WriteProgramCounter(pc);

                                Thread.PopValue();
                                break;
                            }

                        case LeafOpcode.DuplicateValue:
                            {
                                Thread.WriteProgramCounter(pc);

                                Registers.B0_Variant = Thread.PeekValue();
                                Thread.PushValue(Registers.B0_Variant);
                                break;
                            }

                        // MEMORY

                        case LeafOpcode.LoadTableValue:
                            {
                                Registers.B1_TableKey = LeafInstruction.ReadTableKeyPair(block.InstructionStream, ref pc);
                                Thread.WriteProgramCounter(pc);

                                Registers.B0_Variant = Thread.GetVariable(Registers.B1_TableKey, Thread); 
                                Thread.PushValue(Registers.B0_Variant);
                                break;
                            }

                        case LeafOpcode.StoreTableValue:
                            {
                                Registers.B1_TableKey = LeafInstruction.ReadTableKeyPair(block.InstructionStream, ref pc);
                                Thread.WriteProgramCounter(pc);

                                Registers.B0_Variant = Thread.PopValue(); 
                                Thread.SetVariable(Registers.B1_TableKey, Registers.B0_Variant, Thread);
                                break;
                            }

                        case LeafOpcode.IncrementTableValue:
                            {
                                Registers.B1_TableKey = LeafInstruction.ReadTableKeyPair(block.InstructionStream, ref pc);
                                Thread.WriteProgramCounter(pc);

                                Thread.IncrementVariable(Registers.B1_TableKey, 1, Thread);
                                break;
                            }

                        // ARITHMETIC

                        case LeafOpcode.Add:
                            {
                                Thread.WriteProgramCounter(pc);

                                Registers.B0_Variant = Thread.PopValue();
                                Registers.B2_Variant = Thread.PopValue();
                                Registers.B3_Variant = Registers.B0_Variant + Registers.B2_Variant;
                                Thread.PushValue(Registers.B3_Variant);

                                break;
                            }

                        case LeafOpcode.Subtract:
                            {
                                Thread.WriteProgramCounter(pc);

                                Registers.B0_Variant = Thread.PopValue();
                                Registers.B2_Variant = Thread.PopValue();
                                Registers.B3_Variant = Registers.B2_Variant - Registers.B0_Variant;
                                Thread.PushValue(Registers.B3_Variant);

                                break;
                            }

                        case LeafOpcode.Multiply:
                            {
                                Thread.WriteProgramCounter(pc);

                                Registers.B0_Variant = Thread.PopValue();
                                Registers.B2_Variant = Thread.PopValue();
                                Registers.B3_Variant = Registers.B0_Variant * Registers.B2_Variant;
                                Thread.PushValue(Registers.B3_Variant);

                                break;
                            }

                        case LeafOpcode.Divide:
                            {
                                Thread.WriteProgramCounter(pc);

                                Registers.B0_Variant = Thread.PopValue();
                                Registers.B2_Variant = Thread.PopValue();
                                Registers.B3_Variant = Registers.B2_Variant / Registers.B0_Variant;
                                Thread.PushValue(Registers.B3_Variant);

                                break;
                            }

                        // LOGICAL OPERATORS

                        case LeafOpcode.Not:
                            {
                                Thread.WriteProgramCounter(pc);

                                Registers.B0_Variant = Thread.PopValue();
                                Registers.B2_Variant = !Registers.B0_Variant.AsBool();
                                Thread.PushValue(Registers.B2_Variant);
                                break;
                            }

                        case LeafOpcode.CastToBool:
                            {
                                Thread.WriteProgramCounter(pc);

                                Registers.B0_Variant = Thread.PopValue().AsBool();
                                Thread.PushValue(Registers.B0_Variant);
                                break;
                            }

                        case LeafOpcode.LessThan:
                            {
                                Thread.WriteProgramCounter(pc);

                                Registers.B0_Variant = Thread.PopValue();
                                Registers.B2_Variant = Thread.PopValue();
                                Registers.B3_Variant = Registers.B2_Variant < Registers.B0_Variant;
                                Thread.PushValue(Registers.B3_Variant);
                                break;
                            }

                        case LeafOpcode.LessThanOrEqualTo:
                            {
                                Thread.WriteProgramCounter(pc);

                                Registers.B0_Variant = Thread.PopValue();
                                Registers.B2_Variant = Thread.PopValue();
                                Registers.B3_Variant = Registers.B2_Variant <= Registers.B0_Variant;
                                Thread.PushValue(Registers.B3_Variant);
                                break;
                            }

                        case LeafOpcode.EqualTo:
                            {
                                Thread.WriteProgramCounter(pc);

                                Registers.B0_Variant = Thread.PopValue();
                                Registers.B2_Variant = Thread.PopValue();
                                Registers.B3_Variant = Registers.B2_Variant == Registers.B0_Variant;
                                Thread.PushValue(Registers.B3_Variant);
                                break;
                            }

                        case LeafOpcode.NotEqualTo:
                            {
                                Thread.WriteProgramCounter(pc);

                                Registers.B0_Variant = Thread.PopValue();
                                Registers.B2_Variant = Thread.PopValue();
                                Registers.B3_Variant = Registers.B2_Variant != Registers.B0_Variant;
                                Thread.PushValue(Registers.B2_Variant);
                                break;
                            }

                        case LeafOpcode.GreaterThanOrEqualTo:
                            {
                                Thread.WriteProgramCounter(pc);

                                Registers.B0_Variant = Thread.PopValue();
                                Registers.B2_Variant = Thread.PopValue();
                                Registers.B3_Variant = Registers.B2_Variant >= Registers.B0_Variant;
                                Thread.PushValue(Registers.B3_Variant);
                                break;
                            }

                        case LeafOpcode.GreaterThan:
                            {
                                Thread.WriteProgramCounter(pc);

                                Registers.B0_Variant = Thread.PopValue();
                                Registers.B2_Variant = Thread.PopValue();
                                Registers.B3_Variant = Registers.B2_Variant > Registers.B0_Variant;
                                Thread.PushValue(Registers.B3_Variant);
                                break;
                            }
                        
                        // JUMPS

                        case LeafOpcode.Jump:
                            {
                                Registers.B0_JumpShort = LeafInstruction.ReadInt16(block.InstructionStream, ref pc);
                                Thread.WriteProgramCounter(pc);

                                Thread.JumpRelative(Registers.B0_JumpShort);
                                break;
                            }

                        case LeafOpcode.JumpIfFalse:
                            {
                                Registers.B0_JumpShort = LeafInstruction.ReadInt16(block.InstructionStream, ref pc);
                                Thread.WriteProgramCounter(pc);

                                if (!Thread.PopValue().AsBool())
                                {
                                    Thread.JumpRelative(Registers.B0_JumpShort);
                                }
                                break;
                            }

                        case LeafOpcode.JumpIndirect:
                            {
                                Thread.WriteProgramCounter(pc);
                                
                                Registers.B0_JumpLong = Thread.PopValue().AsInt();
                                Thread.JumpRelative(Registers.B0_JumpLong);
                                break;
                            }

                        // FLOW CONTROL

                        case LeafOpcode.GotoNode:
                            {
                                Registers.B1_Identifier = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                                Thread.WriteProgramCounter(pc);

                                TryGotoNode(Plugin, Thread, node, Registers.B1_Identifier);
                                break;
                            }

                        case LeafOpcode.GotoNodeIndirect:
                            {
                                Thread.WriteProgramCounter(pc);

                                Registers.B1_Identifier = Thread.PopValue().AsStringHash();
                                TryGotoNode(Plugin, Thread, node, Registers.B1_Identifier);
                                break;
                            }

                        case LeafOpcode.BranchNode:
                            {
                                Registers.B1_Identifier = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                                Thread.WriteProgramCounter(pc);

                                TryBranchNode(Plugin, Thread, node, Registers.B1_Identifier);
                                break;
                            }

                        case LeafOpcode.BranchNodeIndirect:
                            {
                                Thread.WriteProgramCounter(pc);

                                Registers.B1_Identifier = Thread.PopValue().AsStringHash();
                                TryBranchNode(Plugin, Thread, node, Registers.B1_Identifier);
                                break;
                            }

                        case LeafOpcode.ReturnFromNode:
                            {
                                Thread.PopNode();
                                break;
                            }

                        case LeafOpcode.Stop:
                            {
                                Thread.ClearNodes();
                                break;
                            }

                        case LeafOpcode.Loop:
                            {
                                Thread.ResetProgramCounter();
                                break;
                            }

                        case LeafOpcode.Yield:
                            {
                                Thread.WriteProgramCounter(pc);
                                return true;
                            }

                        // FORKING

                        case LeafOpcode.ForkNode:
                            {
                                Registers.B1_Identifier = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                                Thread.WriteProgramCounter(pc);

                                TryForkNode(Plugin, Thread, node, Registers.B1_Identifier, true);
                                break;
                            }

                        case LeafOpcode.ForkNodeIndirect:
                            {
                                Thread.WriteProgramCounter(pc);
                                
                                Registers.B1_Identifier = Thread.PopValue().AsStringHash();
                                TryForkNode(Plugin, Thread, node, Registers.B1_Identifier, true);
                                break;
                            }

                        case LeafOpcode.ForkNodeUntracked:
                            {
                                Registers.B1_Identifier = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                                Thread.WriteProgramCounter(pc);

                                TryForkNode(Plugin, Thread, node, Registers.B1_Identifier, false);
                                break;
                            }

                        case LeafOpcode.ForkNodeIndirectUntracked:
                            {
                                Thread.WriteProgramCounter(pc);

                                Registers.B1_Identifier = Thread.PopValue().AsStringHash();
                                TryForkNode(Plugin, Thread, node, Registers.B1_Identifier, false);
                                break;
                            }

                        case LeafOpcode.JoinForks:
                            {
                                Thread.WriteProgramCounter(pc);

                                if (Thread.HasChildren())
                                {
                                    State = State_Join;
                                    return true;
                                }
                                break;
                            }

                        // CHOICES

                        case LeafOpcode.AddChoiceOption:
                            {
                                Registers.B1_Identifier = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                                Registers.B0_Byte = LeafInstruction.ReadByte(block.InstructionStream, ref pc);
                                Thread.WriteProgramCounter(pc);

                                if (Thread.PopValue().AsBool())
                                {
                                    Registers.B0_Byte |= (byte) LeafChoice.OptionFlags.IsAvailable;
                                }
                                Registers.B2_Variant = Thread.PopValue();

                                Thread.AddOption(Registers.B2_Variant, Registers.B1_Identifier, (LeafChoice.OptionFlags) Registers.B0_Byte);
                                break;
                            }

                        case LeafOpcode.AddChoiceAnswer:
                            {
                                Registers.B1_Identifier = LeafInstruction.ReadStringHash32(block.InstructionStream, ref pc);
                                Thread.WriteProgramCounter(pc);

                                Registers.B0_Variant = Thread.PopValue();
                                Thread.AddOptionAnswer(Registers.B1_Identifier, Registers.B0_Variant);
                                break;
                            }

                        case LeafOpcode.ShowChoices:
                            {
                                Thread.WriteProgramCounter(pc);

                                choice = Thread.GetOptions();
                                if (choice.AvailableCount > 0)
                                {
                                    Wait = Plugin.ShowOptions(Thread, choice);
                                    if (Wait != null)
                                    {
                                        State = State_Choose;
                                        return true;
                                    }

                                    Registers.B0_Variant = choice.ChosenTarget();
                                    choice.Reset();
                                    Thread.PushValue(Registers.B0_Variant);
                                }
                                else
                                {
                                    choice.Reset();
                                    Thread.PushValue(Variant.Null);
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

                Plugin.OnEnd(Thread);
                return false;
            }

            public void Cleanup()
            {
                State = State_Done;
                IDisposable disposableWait = Wait as IDisposable;
                if (disposableWait != null)
                {
                    disposableWait.Dispose();
                }
                Wait = null;
                Registers = default;
            }

            public void Reset()
            {
                State = State_Default;
                Wait = null;
            }
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