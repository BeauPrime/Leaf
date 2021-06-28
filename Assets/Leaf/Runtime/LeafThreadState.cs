/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    24 Oct 2020
 * 
 * File:    LeafThreadState.cs
 * Purpose: Execution stacks for evaluating leaf nodes.
 */

using BeauUtil;
using BeauUtil.Variants;
using BeauUtil.Debugger;
using BeauRoutine;
using System;
using System.Collections;
using BeauUtil.Tags;

namespace Leaf.Runtime
{
    /// <summary>
    /// Representation of an executing leaf thread.
    /// </summary>
    public abstract class LeafThreadState : ILeafVariableAccess
    {
        private readonly RingBuffer<Variant> m_ValueStack;
        private readonly RingBuffer<LeafThreadHandle> m_Children;
        private readonly LeafChoice m_ChoiceBuffer;
        private readonly VariantTable m_Locals;
        private readonly Action m_KillAction;

        internal uint m_Id;
        internal bool m_Running;

        private string m_Name;
        private float m_QueuedDelay;
        private ILeafActor m_ActorThis;

        protected Routine m_Routine;

        /// <summary>
        /// Variant resolver. Overrides the default resolver in the plugin.
        /// </summary>
        public readonly CustomVariantResolver Resolver;

        /// <summary>
        /// Tagged string. Temporary state for the current string.
        /// </summary>
        public readonly TagString TagString;

        public LeafThreadState()
        {
            m_ValueStack = new RingBuffer<Variant>();
            m_ChoiceBuffer = new LeafChoice();
            m_Locals = new VariantTable();
            m_Children = new RingBuffer<LeafThreadHandle>();

            Resolver = new CustomVariantResolver();
            TagString = new TagString();

            m_KillAction = Kill;
            Resolver.SetDefaultTable(m_Locals);
        }

        /// <summary>
        /// Name of the thread.
        /// </summary>
        public string Name { get { return m_Name; } }

        /// <summary>
        /// Local variable table.
        /// </summary>
        public VariantTable Locals { get { return m_Locals; } }

        /// <summary>
        /// Returns the default "this" object for this thread.
        /// </summary>
        public ILeafActor Actor { get { return m_ActorThis; } }

        IVariantResolver ILeafVariableAccess.Resolver { get { return Resolver; } }

        #region Internal State

        #region Value Stack

        /// <summary>
        /// Pushes a value onto the value stack.
        /// </summary>
        internal void PushValue(Variant inValue)
        {
            m_ValueStack.PushBack(inValue);
        }

        /// <summary>
        /// Peeks the current value on the value stack.
        /// </summary>
        internal Variant PeekValue()
        {
            return m_ValueStack.PeekBack();
        }

        /// <summary>
        /// Pops a value from the value stack.
        /// </summary>
        internal Variant PopValue()
        {
            return m_ValueStack.PopBack();
        }

        #endregion // Stack

        #region Choices

        /// <summary>
        /// Adds an option to the choice buffer.
        /// </summary>
        public void AddOption(Variant inTargetId, StringHash32 inLineCode, bool inbAvailable = true)
        {
            m_ChoiceBuffer.AddOption(new LeafChoice.Option(inTargetId, inLineCode, inbAvailable));
        }

        /// <summary>
        /// Locks and returns the choice buffer.
        /// </summary>
        public LeafChoice GetOptions()
        {
            m_ChoiceBuffer.Offer();
            return m_ChoiceBuffer;
        }

        /// <summary>
        /// Resets the choice buffer.
        /// </summary>
        public void ResetOptions()
        {
            m_ChoiceBuffer.Reset();
        }

        #endregion // Choices

        #region Forks

        /// <summary>
        /// Returns if this thread has any children still running.
        /// </summary>
        public bool HasChildren()
        {
            while(m_Children.Count > 0)
            {
                if (m_Children.PeekBack().IsRunning())
                    return true;
                m_Children.PopBack();
            }

            return false;
        }

        /// <summary>
        /// Adds the given thread as a child of this thread.
        /// </summary>
        public void AddChild(LeafThreadState inThreadState)
        {
            if (inThreadState != null && inThreadState.m_Running)
            {
                m_Children.PushBack(inThreadState.GetHandle());
            }
        }

        /// <summary>
        /// Kills all children.
        /// </summary>
        public void KillChildren()
        {
            while(m_Children.Count > 0)
            {
                var thread = m_Children.PopBack().GetThread();
                if (thread != null)
                    thread.Kill();
            }
        }

        #endregion // Forks

        #region Handles

        public LeafThreadHandle GetHandle()
        {
            return m_Running ? new LeafThreadHandle(this, m_Id) : default(LeafThreadHandle);
        }

        /// <summary>
        /// Returns if this thread is operating with the given id.
        /// </summary>
        public bool HasId(uint inId)
        {
            return m_Running && m_Id == inId;
        }

        #endregion // Handles

        #endregion // Internal State

        #region Lifecycle

        /// <summary>
        /// Sets up the state.
        /// </summary>
        public virtual LeafThreadHandle Setup(string inName, ILeafActor inActor, VariantTable inLocals)
        {
            if (inLocals != null)
            {
                inLocals.CopyTo(m_Locals);
            }
            else
            {
                m_Locals.Clear();
            }

            if (inActor != null && inActor.Locals != null)
            {
                Resolver.SetTable(LeafUtils.ThisIdentifier, inActor.Locals);
            }

            m_Name = inName;
            m_ActorThis = inActor;
            m_Running = true;
            m_Id = (m_Id == uint.MaxValue) ? 1 : m_Id + 1;
            
            return GetHandle();
        }

        /// <summary>
        /// Attaches a routine to this thread.
        /// </summary>
        public virtual void AttachRoutine(Routine inRoutine)
        {
            m_Routine = inRoutine;
            inRoutine.OnComplete(m_KillAction);
            if (m_QueuedDelay > 0)
            {
                m_Routine.DelayBy(m_QueuedDelay);
                m_QueuedDelay = 0;
            }
        }

        #endregion // Lifecycle

        #region Updates

        /// <summary>
        /// If attached to a routine, forces this thread to tick forward.
        /// </summary>
        public void ForceTick()
        {
            m_Routine.TryManuallyUpdate(0);
        }

        /// <summary>
        /// Returns if the thread is running or paused.
        /// </summary>
        public bool IsRunning() { return m_Running; }

        /// <summary>
        /// Pauses the thread.
        /// </summary>
        public void Pause() { m_Routine.Pause(); }

        /// <summary>
        /// Resumes the thread.
        /// </summary>
        public void Resume() { m_Routine.Resume(); }

        /// <summary>
        /// Returns if the thread is paused.
        /// </summary>
        public bool IsPaused() { return m_Routine.GetPaused(); }

        /// <summary>
        /// Waits for the thread to be completed.
        /// </summary>
        public IEnumerator Wait() { return m_Routine.Wait(); }

        /// <summary>
        /// Delays execution of the thread.
        /// </summary>
        public void DelayBy(float inDelaySeconds)
        {
            if (m_Routine)
            {
                m_Routine.DelayBy(inDelaySeconds);
            }
            else
            {
                m_QueuedDelay += inDelaySeconds;
            }
        }

        #endregion // Updates

        #region Lookups

        /// <summary>
        /// Attempts to look up an object from the given id.
        /// </summary>
        public abstract bool TryLookupObject(StringHash32 inId, out object outObject);

        #endregion // Lookups

        #region Cleanup

        /// <summary>
        /// Kills this thread.
        /// </summary>
        public void Kill()
        {
            if (m_Running)
            {
                Reset();
            }
        }

        /// <summary>
        /// Resets all thread state.
        /// </summary>
        protected virtual void Reset()
        {
            m_Running = false;

            KillChildren();
            
            m_ValueStack.Clear();
            m_ChoiceBuffer.Reset();
            m_Children.Clear();
            m_ChoiceBuffer.Reset();
            Resolver.Clear();
            m_Locals.Clear();
            m_Name = null;
            m_QueuedDelay = 0;
            m_ActorThis = null;
            Resolver.ClearTable(LeafUtils.ThisIdentifier);

            m_Routine.Stop();
        }

        #endregion // Cleanup
    }

    /// <summary>
    /// Leaf thread
    /// </summary>
    public class LeafThreadState<TNode> : LeafThreadState
        where TNode : LeafNode
    {
        #region Types

        private struct Frame
        {
            public TNode Node;
            public int ProgramCounter;

            public Frame(TNode inNode)
            {
                Node = inNode;
                ProgramCounter = -1;
            }
        }

        #endregion // Types
        
        private readonly RingBuffer<Frame> m_FrameStack;
        private ILeafPlugin<TNode> m_Plugin;

        public LeafThreadState(ILeafPlugin<TNode> inPlugin)
        {
            if (inPlugin == null)
                throw new ArgumentNullException("inPlugin");

            m_FrameStack = new RingBuffer<Frame>();
            m_Plugin = inPlugin;
            Resolver.Base = inPlugin.Resolver;
        }

        #region Internal State

        #region Program Counters

        internal void AdvanceState(out TNode outNode, out int outProgramCounter)
        {
            ref Frame currentFrame = ref m_FrameStack[0];
            outNode = currentFrame.Node;
            outProgramCounter = ++currentFrame.ProgramCounter;
        }

        internal void JumpRelative(int inJump)
        {
            m_FrameStack[0].ProgramCounter += (inJump - 1);
        }

        internal void JumpAbsolute(int inIndex)
        {
            m_FrameStack[0].ProgramCounter = inIndex - 1;
        }

        internal void ResetProgramCounter()
        {
            m_FrameStack[0].ProgramCounter = -1;
        }

        #endregion // Program Counters

        #region Node Stack

        /// <summary>
        /// Pushes the given node onto the frame stack.
        /// </summary>
        public void PushNode(TNode inNode)
        {
            m_FrameStack.PushFront(new Frame(inNode));
            m_Plugin?.OnNodeEnter(inNode, this);
        }

        /// <summary>
        /// Peeks the node at the top of the frame stack.
        /// </summary>
        public TNode PeekNode()
        {
            return m_FrameStack.PeekFront().Node;
        }

        /// <summary>
        /// Pops the current node from the frame stack.
        /// </summary>
        public void PopNode()
        {
            TNode node = m_FrameStack.PopFront().Node;
            m_Plugin?.OnNodeExit(node, this);
        }

        /// <summary>
        /// Pops the current node from the frame stack
        /// and inserts the given node.
        /// </summary>
        public void GotoNode(TNode inNode)
        {
            if (m_FrameStack.Count > 0)
            {
                PopNode();
            }

            if (inNode != null)
            {
                PushNode(inNode);
            }
        }

        /// <summary>
        /// Returns if there are any nodes in the frame stack.
        /// </summary>
        public bool HasNodes()
        {
            return m_FrameStack.Count > 0;
        }

        /// <summary>
        /// Flushes all nodes from the frame stack.
        /// </summary>
        public void ClearNodes()
        {
            while(m_FrameStack.Count > 0)
            {
                PopNode();
            }
        }

        #endregion // Node Stack

        #endregion // Internal State

        #region Lookups

        public override bool TryLookupObject(StringHash32 inId, out object outObject)
        {
            if (inId.IsEmpty)
            {
                outObject = null;
                return true;
            }

            if (inId == LeafUtils.ThisIdentifier)
            {
                outObject = Actor;
                return true;
            }

            if (inId == LeafUtils.ThreadIdentifier)
            {
                outObject = this;
                return true;
            }

            return m_Plugin.TryLookupObject(inId, this, out outObject);
        }

        #endregion // Lookups

        #region Cleanup

        /// <summary>
        /// Resets thread state.
        /// </summary>
        protected override void Reset()
        {
            base.Reset();

            ClearNodes();
        }

        #endregion // Cleanup
    }
}