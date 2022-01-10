/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    24 Oct 2020
 * 
 * File:    LeafChoice.cs
 * Purpose: Set of options to display, linked to different nodes.
 */

#if CSHARP_7_3_OR_NEWER
#define EXPANDED_REFS
#endif // CSHARP_7_3_OR_NEWER

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
            public OptionFlags Flags;
            public bool IsAvailable { get { return (Flags & OptionFlags.IsAvailable) != 0; } }

            internal ushort m_AnswersOffset;
            internal ushort m_AnswersLength;
            internal Variant m_DefaultAnswerTarget;

            public Option(Variant inTargetId, StringHash32 inLineCode, OptionFlags inFlags = OptionFlags.IsAvailable)
            {
                TargetId = inTargetId;
                LineCode = inLineCode;
                Flags = inFlags;

                m_AnswersOffset = 0;
                m_AnswersLength = 0;
                m_DefaultAnswerTarget = null;
            }
        }

        /// <summary>
        /// Answer selector within a special option.
        /// </summary>
        public struct Answer
        {
            public readonly Variant AnswerId;
            public readonly Variant TargetId;

            public Answer(Variant inAnswerId, Variant inTargetId)
            {
                AnswerId = inAnswerId;
                TargetId = inTargetId;
            }
        }

        /// <summary>
        /// Flags describing intended option behavior.
        /// </summary>
        [Flags]
        public enum OptionFlags : byte
        {
            IsAvailable = 0x01,
            IsSelector = 0x02
        }

        private enum State
        {
            Accumulating,
            Choosing,
            Chosen
        }

        private readonly RingBuffer<Option> m_AllOptions = new RingBuffer<Option>(4, RingBufferMode.Expand);
        private readonly RingBuffer<Answer> m_AllAnswers = new RingBuffer<Answer>(4, RingBufferMode.Expand);
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
            inOption.m_AnswersOffset = (ushort) m_AllAnswers.Count;
            m_AllOptions.PushBack(inOption);
            if (inOption.IsAvailable)
            {
                ++AvailableCount;
            }
        }

        /// <summary>
        /// Adds an answer to the last option.
        /// </summary>
        public void AddAnswer(Answer inAnswer)
        {
            if (m_State != State.Accumulating)
                throw new InvalidOperationException(string.Format("Cannot add answers while in {0} state", m_State));
            if (m_AllOptions.Count == 0)
                throw new InvalidOperationException("Cannot add answers when no choices have been added");

            Variant answerId = inAnswer.AnswerId;

            // increment previous choice answer count
            #if EXPANDED_REFS
            ref Option option = ref m_AllOptions[m_AllOptions.Count - 1];
            #else
            Option option = m_AllOptions[m_AllOptions.Count - 1];
            #endif // EXPANDED_REFS

            option.Flags |= OptionFlags.IsSelector;

            if (answerId.IsNull() || answerId.AsStringHash().IsEmpty)
            {
                option.m_DefaultAnswerTarget = inAnswer.TargetId;
            }
            else
            {
                m_AllAnswers.PushBack(inAnswer);
                option.m_AnswersLength++;
            }
            
            #if !EXPANDED_REFS
            m_AllOptions[m_AllOptions.Count - 1] = option;
            #endif // !EXPANDED_REFS
        }

        /// <summary>
        /// Returns if an option with the given target exists.
        /// </summary>
        public bool HasOption(Variant inTargetId)
        {
            return IndexOf(inTargetId) >= 0;
        }

        /// <summary>
        /// Returns if an answer with the given target id and answer id exists.
        /// </summary>
        public bool HasAnswer(Variant inTargetId, Variant inAnswerId)
        {
            int index = IndexOf(inTargetId);
            if (index >= 0)
            {
                bool bDefault;
                GetAnswerResponse(index, inAnswerId, out bDefault);
                return !bDefault;
            }

            return false;
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
        /// Chooses the option with the given target id.
        /// </summary>
        public void Choose(Variant inTargetId)
        {
            if (m_State != State.Choosing)
                throw new InvalidOperationException(string.Format("Cannot choose an option while in {0} state", m_State));

            int index = IndexOf(inTargetId);
            if (index >= 0)
            {
                m_ChosenIndex = index;
                m_ChosenOption = inTargetId;
                m_State = State.Chosen;
                return;
            }

            throw new Exception(string.Format("No option with target id {0} is present in this choice", inTargetId.ToDebugString()));
        }

        /// <summary>
        /// Chooses the option with the given target id and answer id.
        /// </summary>
        public void Choose(Variant inTargetId, Variant inAnswerId)
        {
            if (m_State != State.Choosing)
                throw new InvalidOperationException(string.Format("Cannot choose an option while in {0} state", m_State));

            int index = IndexOf(inTargetId);
            if (index >= 0)
            {
                bool _;
                m_ChosenIndex = index;
                m_ChosenOption = GetAnswerResponse(index, inAnswerId, out _);
                m_State = State.Chosen;
                return;
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
        /// Chooses the option with the given index and answer id.
        /// </summary>
        public void Choose(int inIndex, Variant inAnswerId)
        {
            if (m_State != State.Choosing)
                throw new InvalidOperationException(string.Format("Cannot choose an option while in {0} state", m_State));
            if (inIndex < 0 || inIndex >= m_AllOptions.Count)
                throw new ArgumentOutOfRangeException("inIndex");
            
            bool _;
            m_ChosenIndex = inIndex;
            m_ChosenOption = GetAnswerResponse(inIndex, inAnswerId, out _);
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
        /// Returns the chosen option's target id.
        /// </summary>
        public Variant ChosenTarget()
        {
            return m_ChosenOption;
        }

        /// <summary>
        /// Returns the index of the chosen option.
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
            m_AllAnswers.Clear();
            AvailableCount = 0;
            m_State = State.Accumulating;
            m_ChosenOption = Variant.Null;
            m_ChosenIndex = -1;
        }

        private int IndexOf(Variant inTargetId)
        {
            for(int i = 0, length = m_AllOptions.Count; i < length; i++)
            {
                if (m_AllOptions[i].TargetId == inTargetId)
                {
                    return i;
                }
            }

            return -1;
        }

        private Variant GetAnswerResponse(int inOptionIndex, Variant inAnswer, out bool outbFound)
        {
            Option option = m_AllOptions[inOptionIndex];
            ListSlice<Answer> answers = new ListSlice<Answer>(m_AllAnswers, option.m_AnswersOffset, option.m_AnswersLength);
            Answer check;
            for(int i = 0; i < answers.Length; i++)
            {
                check = answers[i];
                if (check.AnswerId == inAnswer)
                {
                    outbFound = true;
                    return check.TargetId;
                }
            }

            outbFound = false;
            return option.m_DefaultAnswerTarget;
        }
    }
}