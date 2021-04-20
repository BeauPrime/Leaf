/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    24 Oct 2020
 * 
 * File:    LeafChoice.cs
 * Purpose: Set of options to display, linked to different nodes.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using BeauUtil;
using BeauUtil.Variants;

namespace Leaf
{
    /// <summary>
    /// A set of options to display.
    /// </summary>
    public class LeafChoice : IReadOnlyList<LeafChoice.Option>
    {
        /// <summary>
        /// Single option within a choice.
        /// </summary>
        public struct Option
        {
            public readonly Variant TargetId;
            public readonly StringHash32 LineCode;
            public readonly bool IsAvailable;

            public Option(Variant inTargetId, StringHash32 inLineCode, bool inbIsAvailable = true)
            {
                TargetId = inTargetId;
                LineCode = inLineCode;
                IsAvailable = inbIsAvailable;
            }
        }

        private enum State
        {
            Accumulating,
            Choosing,
            Chosen
        }

        private readonly List<Option> m_AllOptions = new List<Option>(4);
        private State m_State = State.Accumulating;
        private Variant m_ChosenOption;
        private int m_ChosenIndex;

        #region IReadOnlyList

        public int Count { get { return m_AllOptions.Count; } }

        public int AvailableCount { get; private set; }

        public Option this[int index]
        {
            get { return m_AllOptions[index]; }
        }

        public IEnumerable<Option> AvailableOptions()
        {
            for(int i = 0; i < m_AllOptions.Count; ++i)
            {
                Option op = m_AllOptions[i];
                if (op.IsAvailable)
                    yield return op;
            }
        }

        public IEnumerator<Option> GetEnumerator()
        {
            return m_AllOptions.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion // IReadOnlyList

        /// <summary>
        /// Adds an option to the choice.
        /// </summary>
        public void AddOption(Option inOption)
        {
            if (m_State != State.Accumulating)
                throw new InvalidOperationException(string.Format("Cannot add options while in {0} state", m_State));
            m_AllOptions.Add(inOption);
            if (inOption.IsAvailable)
            {
                ++AvailableCount;
            }
        }

        /// <summary>
        /// Locks options in place to present.
        /// </summary>
        public void Offer()
        {
            if (m_State != State.Accumulating)
                throw new InvalidOperationException(string.Format("Cannot offer options while in {0} state", m_State));
            m_State = State.Choosing;
        }

        /// <summary>
        /// Chooses the option with the given node id.
        /// </summary>
        public void Choose(Variant inTargetId)
        {
            if (m_State != State.Choosing)
                throw new InvalidOperationException(string.Format("Cannot choose an option while in {0} state", m_State));

            for(int i = m_AllOptions.Count - 1; i >= 0; --i)
            {
                if (m_AllOptions[i].TargetId == inTargetId)
                {
                    m_ChosenIndex = i;
                    m_ChosenOption = inTargetId;
                    m_State = State.Chosen;
                    return;
                }
            }

            throw new Exception(string.Format("No option with target id {0} is present in this choice", inTargetId.ToDebugString()));
        }

        /// <summary>
        /// Chooses the option with the given index.
        /// </summary>
        public void Choose(int inIndex)
        {
            if (m_State != State.Choosing)
                throw new InvalidOperationException(string.Format("Cannot choose an option while in {0} state", m_State));
            if (inIndex < 0 || inIndex >= m_AllOptions.Count)
                throw new ArgumentOutOfRangeException("inIndex");
            
            m_ChosenIndex = inIndex;
            m_ChosenOption = m_AllOptions[inIndex].TargetId;
            m_State = State.Chosen;
        }

        /// <summary>
        /// Returns if an option has been chosen.
        /// </summary>
        public bool HasChosen()
        {
            return m_State == State.Chosen;
        }

        /// <summary>
        /// Returns the chosen option's node.
        /// </summary>
        public Variant ChosenTarget()
        {
            return m_ChosenOption;
        }

        /// <summary>
        /// Returns the index of the option.
        /// </summary>
        public int ChosenIndex()
        {
            return m_ChosenIndex;
        }

        /// <summary>
        /// Resets choice state.
        /// </summary>
        public void Reset()
        {
            m_AllOptions.Clear();
            AvailableCount = 0;
            m_State = State.Accumulating;
            m_ChosenOption = Variant.Null;
            m_ChosenIndex = -1;
        }
    }
}