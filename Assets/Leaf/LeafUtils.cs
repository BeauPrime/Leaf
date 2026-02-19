/*
 * Copyright (C) 2021. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    6 June 2021
 * 
 * File:    LeafUtils.cs
 * Purpose: Leaf utility methods.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using BeauUtil.Tags;
using BeauUtil.Variants;
using Leaf.Runtime;
using UnityEngine;

namespace Leaf
{
    /// <summary>
    /// Leaf utility methods
    /// </summary>
    static public class LeafUtils
    {
        #region Consts

        /// <summary>
        /// Special "this" identifier.
        /// </summary>
        static public readonly StringHash32 ThisIdentifier = "this";

        /// <summary>
        /// Special "thread" identifier.
        /// </summary>
        static public readonly StringHash32 ThreadIdentifier = "thread";

        /// <summary>
        /// Special "locals" identifier.
        /// </summary>
        static public readonly StringHash32 LocalIdentifier = "local";

        /// <summary>
        /// Built-in events.
        /// </summary>
        static public class Events
        {
            /// <summary>
            /// Wait event. Will pause for a certain number of seconds.
            /// {wait [seconds]}
            /// </summary>
            static public readonly StringHash32 Wait = "_wait";

            /// <summary>
            /// Character event. Will specify a character and, optionally, a pose
            /// {@characterId} or {@characterId #poseId}
            /// </summary>
            static public readonly StringHash32 Character = "_character-id";

            /// <summary>
            /// Pose event. Will specify a pose for the current character.
            /// {#poseId}
            /// </summary>
            static public readonly StringHash32 Pose = "_character-pose";
        }

        /// <summary>
        /// Built-in replace tags.
        /// </summary>
        static public class ReplaceTags
        {
            /// <summary>
            /// Selects a random string to substitute.
            /// {random contentA|contentB} or {random contentA|contentB|contentC}, etc.
            /// </summary>
            static public readonly StringHash32 Random = "_random";
        }

        static internal readonly Type ActorType = typeof(ILeafActor);

        #endregion // Consts

        #region Identifiers

        /// <summary>
        /// Returns if the given node identifier is valid.
        /// </summary>
        static public bool IsValidIdentifier(StringSlice inIdentifier)
        {
            return VariantUtils.IsValidIdentifier(inIdentifier);
        }

        static internal string AssembleFullId(StringBuilder ioBuilder, StringSlice inRoot, StringSlice inId, char inSeparator)
        {
            if (inId.StartsWith(inSeparator))
            {
                inId = inId.Substring(1);
            }

            if (!inRoot.IsEmpty)
            {
                if (inRoot.EndsWith(inSeparator))
                {
                    inRoot = inRoot.Substring(0, inRoot.Length - 1);
                }
                
                ioBuilder.AppendSlice(inRoot);
                ioBuilder.Append(inSeparator);
                ioBuilder.AppendSlice(inId);
                return ioBuilder.Flush();
            }
            
            return inId.ToString();
        }

        #endregion // Identifiers

        #region Method Cache

        /// <summary>
        /// Creates a new method cache for use by a leaf plugin.
        /// </summary>
        static public MethodCache<LeafMember> CreateMethodCache()
        {
            return new MethodCache<LeafMember>(typeof(MonoBehaviour), new LeafStringConverter());
        }

        /// <summary>
        /// Creates a new method cache for use by a leaf plugin.
        /// </summary>
        static public MethodCache<LeafMember> CreateMethodCache(Type inComponentType)
        {
            return new MethodCache<LeafMember>(inComponentType, new LeafStringConverter());
        }

        #endregion // Method Cache

        #region Default Leaf Members

        /// <summary>
        /// Waits for the given number of seconds.
        /// </summary>
        [Obsolete("LeafUtils.Wait has been replaced by the $wait command")]
        static public void Wait([BindContext] LeafEvalContext inContext, float inSeconds)
        {
            inContext.Thread.DelayBy(inSeconds);
            inContext.Thread.Interrupt();
        }

        /// <summary>
        /// Waits for the given number of seconds.
        /// This is in real time and does not account for time scale.
        /// </summary>
        [LeafMember("WaitAbs"), UnityEngine.Scripting.Preserve]
        static public IEnumerator WaitAbs(float inSeconds)
        {
            return Routine.WaitRealSeconds(inSeconds);
        }

        /// <summary>
        /// Returns if a random value between 0 and 1 is greater than or equal to the provided fraction.
        /// </summary>
        [LeafMember("Chance"), UnityEngine.Scripting.Preserve]
        static private bool Chance([BindContext] LeafEvalContext inContext, float inFraction)
        {
            return inContext.Plugin.RandomFloat(0, 1) >= inFraction;
        }

        #endregion // Default Leaf Members

        #region Default Parsing Configs

        private class DefaultParseContext
        {
            public readonly ILeafPlugin Plugin;
            public readonly LocalizeDelegate Localize;
            public RingBuffer<DefaultRandomState> RandomStates;

            internal DefaultParseContext(ILeafPlugin inPlugin, LocalizeDelegate inLocalize)
            {
                Plugin = inPlugin;
                Localize = inLocalize;
            }
        }

        /// <summary>
        /// Delegate for performing localization.
        /// </summary>
        public delegate string LocalizeDelegate(StringHash32 inHash, object inContext);

        /// <summary>
        /// Configures default parsers.
        /// </summary>
        static public void ConfigureDefaultParsers(CustomTagParserConfig inConfig, ILeafPlugin inPlugin, LocalizeDelegate inLocalizationDelegate)
        {
            DefaultParseContext parseContext = new DefaultParseContext(inPlugin, inLocalizationDelegate);

            inConfig.AddReplace("$*", (t, o) => ReplaceOperandPlugin(t, o, parseContext));
            inConfig.AddReplace("loc ", (t, o) => ReplaceLocPlugin(t, o, parseContext));
            inConfig.AddReplace("rand", (t, o) => ReplaceRandomPlugin(t, o, parseContext)).WithAliases("random");
            // inConfig.AddReplace("select", (t, o) => ReplaceSelectPlugin(t, o, parseContext));

            // default event types

            inConfig.AddEvent("wait", Events.Wait).WithFloatData(0.25f);
            inConfig.AddEvent("@*", Events.Character).ProcessWith(ParseCharacterArgument);
            inConfig.AddEvent("#*", Events.Pose).ProcessWith(ParsePoseArgument);
        }

        /// <summary>
        /// Configures default handlers
        /// </summary>
        static public void ConfigureDefaultHandlers(TagStringEventHandler inHandler, ILeafPlugin inPlugin)
        {
            inHandler.Register(Events.Wait, (eData, context) => Routine.WaitSeconds(eData.GetFloat()));
        }

        /// <summary>
        /// Attempts to find the character id for a specific line.
        /// </summary>
        static public bool TryFindCharacterId(TagString inTag, out StringHash32 outCharacterId)
        {
            TagEventData evtData;
            if (inTag.TryFindEvent(Events.Character, out evtData))
            {
                outCharacterId = evtData.Argument0.AsStringHash();
                return true;
            }

            outCharacterId = default;
            return false;
        }

        /// <summary>
        /// Attempts to find the pose id for a specific line.
        /// </summary>
        static public bool TryFindPoseId(TagString inTag, out StringHash32 outPoseId)
        {
            TagEventData evtData;
            if (inTag.TryFindEvent(Events.Pose, out evtData))
            {
                outPoseId = evtData.Argument0.AsStringHash();
                return true;
            }

            if (inTag.TryFindEvent(Events.Character, out evtData))
            {
                outPoseId = evtData.Argument1.AsStringHash();
                return true;
            }

            outPoseId = default;
            return false;
        }

        static private void ParseCharacterArgument(TagData inTag, object inContext, ref TagEventData ioData)
        {
            ioData.Argument0 = inTag.Id.Substring(1).Hash32();
            if (inTag.Data.StartsWith('#'))
                ioData.Argument1 = inTag.Data.Substring(1).Hash32();
        }

        static private void ParsePoseArgument(TagData inTag, object inContext, ref TagEventData ioData)
        {
            ioData.Argument0 = inTag.Id.Substring(1).Hash32();
        }

        static private string ReplaceOperandPlugin(StringSlice inSource, object inContext, DefaultParseContext inParseContext)
        {
            StringSlice data = inSource.Substring(1);
            StringSlice type = null; // TODO: Determine formatting specifiers
            int formatSpecifierIdx = data.IndexOf('|');
            if (formatSpecifierIdx >= 0)
            {
                type = data.Substring(formatSpecifierIdx + 1).Trim();
                data = data.Substring(0, formatSpecifierIdx).TrimEnd();
            }

            LeafEvalContext context = LeafEvalContext.FromObject(inContext, inParseContext.Plugin);

            Variant value = default;
            if (!TryResolveVariant(context, data, out value))
            {
                return GetDisplayedErrorString(inSource);
            }

            if (type == "i" || type == "int")
            {
                return value.AsInt().ToString();
            }
            else if (type == "f" || type == "float")
            {
                return value.AsFloat().ToString();
            }
            else if (type == "b" || type == "bool")
            {
                return value.AsBool().ToString();
            }
            else if (type == "loc")
            {
                if (inParseContext.Localize != null)
                {
                    return inParseContext.Localize(value.AsStringHash(), inContext);
                }
                else
                {
                    Log.Error("[LeafUtils] 'loc' argument provided to inline leaf operand '{0}', but no localization callback was provided", inSource);
                    return GetDisplayedErrorString(inSource);
                }
            }
            else
            {
                return value.ToString();
            }
        }

        static private unsafe string ReplaceRandomPlugin(TagData inTag, object inContext, DefaultParseContext inParseContext)
        {
            CachedListParseResources resources = (s_ListParseResources ?? (s_ListParseResources = new CachedListParseResources()));
            var list = resources.ArgsList;
            int count = inTag.Data.Split(resources.PipeSplitter, StringSplitOptions.None, resources.ArgsList);

            LeafEvalContext context = LeafEvalContext.FromObject(inContext, inParseContext.Plugin);
            StringSlice returnValue = null;

            if (count == 0)
            {
                returnValue = GetDisplayedErrorString(inTag);
            }
            else if (count == 1)
            {
                returnValue = list[0];
            }
            else
            {
                int* indexBuffer = stackalloc int[count];
                for(int i = 0; i < count; i++)
                    indexBuffer[i] = i;
                
                int iterations;
                int randomIdx = StaticRandom.Prepare(StringHash32.Fast(inTag.Data), inParseContext, count, out iterations);
                
                for(int i = 0; i <= iterations; i++)
                {
                    int next = StaticRandom.Random16(randomIdx, inParseContext) % count;
                    int stringIdx = indexBuffer[next];
                    
                    if (i == iterations)
                    {
                        returnValue = list[stringIdx];
                    }
                    else
                    {
                        FastRemove(indexBuffer, ref count, stringIdx);
                    }
                }
            }

            list.Clear();
            return returnValue.ToString();
        }

        static private unsafe bool FastRemove(int* ioBuffer, ref int ioCount, int inElement)
        {
            for(int i = 0; i < ioCount; i++)
            {
                if (ioBuffer[i] == inElement)
                {
                    if (i < ioCount - 1)
                    {
                        ioBuffer[i] = ioBuffer[ioCount - 1];
                    }
                    ioCount--;
                    return true;
                }
            }

            return false;
        }

        // TODO: Finish implementing
        // static private string ReplaceSelectPlugin(StringSlice inSource, object inContext, DefaultParseContext inParseContext)
        // {
        //     LeafEvalContext context = LeafEvalContext.FromObject(inContext, inParseContext.Plugin);

        //     StringSlice 
        //     int separatorIdx = inSource.IndexOf(';');

        //     Variant value = default;
        //     if (!TryResolveVariant(context, data, out value))
        //     {
        //         return GetDisplayedErrorString(inSource);
        //     }
        // }

        static private string ReplaceLocPlugin(TagData inTag, object inContext, DefaultParseContext inParseContext)
        {
            if (inParseContext.Localize != null)
            {
                return inParseContext.Localize(inTag.Data, inContext);
            }
            else
            {
                Log.Error("[LeafUtils] 'loc' argument provided to inline leaf operand '{0}', but no localization callback was provided", inTag);
                return GetDisplayedErrorString(inTag);
            }
        }

        static private string GetDisplayedErrorString(StringSlice inData)
        {
            return string.Format("<color=red>ERROR: {0}</color>", inData.ToString());
        }

        static private string GetDisplayedErrorString(TagData inData)
        {
            return string.Format("<color=red>ERROR: {0}</color>", inData.ToString());
        }

        #endregion // Default Parsing Configs

        #region Random

        private struct DefaultRandomState
        {
            public StringHash32 Id;
            public ushort FixedSeed;
            public ushort A;
            public ushort B;
            public ushort VisitCount;
        }

        static private class StaticRandom
        {
            static internal int Prepare(StringHash32 inId, DefaultParseContext inParseContext, int inVisitCountPeriod, out int outVisitCount)
            {
                if (inVisitCountPeriod == 0)
                {
                    inVisitCountPeriod = 1;
                }

                var buffer = inParseContext.RandomStates ?? (inParseContext.RandomStates = new RingBuffer<DefaultRandomState>(32, RingBufferMode.Overwrite));
                DefaultRandomState state;
                for(int i = 0; i < buffer.Count; i++)
                {
                    state = buffer[i];
                    if (state.Id == inId)
                    {
                        RandomSeed(ref state, inId, inVisitCountPeriod);
                        outVisitCount = state.VisitCount % inVisitCountPeriod;
                        state.VisitCount++;
                        buffer[i] = state;
                        return i;
                    }
                }
                {
                    state = NewRandom(inId, inParseContext.Plugin, inVisitCountPeriod);
                    outVisitCount = state.VisitCount % inVisitCountPeriod;
                    state.VisitCount++;
                    buffer.PushBack(state);
                    return buffer.Count - 1;
                }
            }

            static internal ushort Random16(int inHandle, DefaultParseContext inParseContext)
            {
                var buffer = inParseContext.RandomStates ?? (inParseContext.RandomStates = new RingBuffer<DefaultRandomState>(32, RingBufferMode.Overwrite));
                DefaultRandomState state = buffer[inHandle];
                ushort val = NextRandom(ref state);
                buffer[inHandle] = state;
                return val;
            }

            static private DefaultRandomState NewRandom(StringHash32 inId, ILeafPlugin inPlugin, int inVisitCountPeriod)
            {
                DefaultRandomState state = default;
                state.Id = inId;
                state.FixedSeed = (ushort) inPlugin.RandomInt(1, ushort.MaxValue);
                RandomSeed(ref state, inId, inVisitCountPeriod);
                return state;
            }

            // Random Generation Algorithm from: http://b2d-f9r.blogspot.com/2010/08/16-bit-xorshift-rng-now-with-more.html
            static private ushort NextRandom(ref DefaultRandomState ioState)
            {
                ushort t = (ushort) (ioState.B ^ (ioState.B << 5));
                ioState.B = t;
                return ioState.A = (ushort) ((ioState.A ^ (ioState.A >> 1)) ^ (t ^ (t >> 3)));
            }

            static private void RandomSeed(ref DefaultRandomState ioState, StringHash32 inId, int inPeriod)
            {
                int periodStart = inPeriod * (ioState.VisitCount / inPeriod);
                ioState.A = 1;
                ioState.B = (ushort) (1 + inId.GetHashCode() + periodStart);
            }
        }

        #endregion // Random
        
        #region Resolution

        /// <summary>
        /// Attempts to handles inline argument syntax.
        /// </summary>
        static public bool TryHandleInline(LeafEvalContext inContext, ref StringSlice inData, out Variant outObject)
        {
            if (inData.Length >= 2 && inData[0] == '$')
            {
                StringSlice inner = inData.Substring(1);
                if (inner[0] == '$')
                {
                    inData = inner;
                    outObject = Variant.Null;
                    return false;
                }

                Variant returnVal;
                if (!TryResolveVariant(inContext, inner, out returnVal))
                {
                    outObject = Variant.Null;
                    return false;
                }

                outObject = returnVal;
                return true;
            }
            else
            {
                outObject = Variant.Null;
                return false;
            }
        }

        /// <summary>
        /// Attempts to handles inline argument syntax.
        /// </summary>
        static public bool TryParseArgument(LeafEvalContext inContext, StringSlice inData, Type inType, out NonBoxedValue outObject)
        {
            if (inData.Length >= 2 && inData[0] == '$')
            {
                StringSlice inner = inData.Substring(1);
                if (inner[0] == '$')
                {
                    return StringParser.TryConvertTo(inner, inType, out outObject);
                }

                Variant returnVal;
                if (!TryResolveVariant(inContext, inner, out returnVal))
                {
                    outObject = null;
                    return false;
                }

                if (ActorType.IsAssignableFrom(inType))
                {
                    object actor;
                    inContext.Plugin.TryLookupObject(returnVal.AsStringHash(), inContext.Thread, out actor);
                    outObject = new NonBoxedValue(actor);
                    return true;
                }

                return Variant.TryConvertTo(returnVal, inType, out outObject);
            }
            else
            {
                if (ActorType.IsAssignableFrom(inType))
                {
                    object actor;
                    inContext.Plugin.TryLookupObject(inData.Hash32(), inContext.Thread, out actor);
                    outObject = new NonBoxedValue(actor);
                    return true;
                }

                return StringParser.TryConvertTo(inData, inType, out outObject);
            }
        }

        /// <summary>
        /// Attempts to handle inline argument syntax.
        /// </summary>
        static public bool TryParseArgument<T>(LeafEvalContext inContext, StringSlice inData, out T outObject)
        {
            NonBoxedValue ret;
            bool bSuccess = TryParseArgument(inContext, inData, typeof(T), out ret);
            if (bSuccess)
            {
                outObject = (T) ret.AsObject();
                return true;
            }

            outObject = default;
            return false;
        }

        /// <summary>
        /// Handles inline argument syntax.
        /// </summary>
        static public T ParseArgument<T>(LeafEvalContext inContext, StringSlice inData, T inDefault = default(T))
        {
            T val;
            if (!TryParseArgument<T>(inContext, inData, out val))
                val = inDefault;
            return val;
        }

        /// <summary>
        /// Attempts to resolve an inline variant from the given string.
        /// </summary>
        static public bool TryResolveVariant(LeafEvalContext inContext, StringSlice inSource, out Variant outValue)
        {
            VariantOperand operand;
            if (!VariantOperand.TryParse(inSource, out operand))
            {
                Log.Error("[LeafUtils] Unable to parse operand '{0}' to operand", inSource);
                outValue = Variant.Null;
                return false;
            }

            Variant value = Variant.Null;

            switch(operand.Type)
            {
                case VariantOperand.Mode.Variant:
                    {
                        value = operand.Value;
                        break;
                    }

                case VariantOperand.Mode.TableKey:
                    {
                        TableKeyPair keyPair = operand.TableKey;
                        bool bFound = false;
                        VariantTable table = inContext.Table;
                        if (table != null && (keyPair.TableId.IsEmpty || keyPair.TableId == table.Name))
                        {
                            bFound = table.TryLookup(keyPair.VariableId, out value);
                        }

                        if (!bFound)
                        {
                            bFound = inContext.Resolver.TryResolve(keyPair, out value);
                        }
                        break;
                    }

                case VariantOperand.Mode.Method:
                    {
                        NonBoxedValue rawObj;
                        if (!inContext.MethodCache.TryStaticInvoke(operand.MethodCall, inContext, out rawObj))
                        {
                            Log.Error("[LeafUtils] Unable to execute {0} in inline method call '{1}'", operand.MethodCall, inSource);
                            outValue = Variant.Null;
                            return false;
                        }

                        if (!Variant.TryConvertFrom(rawObj, out value))
                        {
                            Log.Error("[LeafUtils] Unable to convert result of {0} ({1}) to Variant in inline method call '{2}'", operand.MethodCall, rawObj, inSource);
                            outValue = Variant.Null;
                            return false;
                        }
                        break;
                    }

                default:
                    throw new IndexOutOfRangeException("Unknown VariantOperand type " + operand.Type);
            }

            outValue = value;
            return true;
        }

        #endregion // Resolution
    
        #region Node Parsing

        private sealed class CachedListParseResources
        {
            public readonly StringSlice.ISplitter Splitter = new StringUtils.ArgsList.Splitter();
            public readonly StringSlice.ISplitter PipeSplitter = new StringUtils.ArgsList.Splitter('|');
            public readonly List<StringSlice> ArgsList = new List<StringSlice>(16);
        }

        [ThreadStatic] static private CachedListParseResources s_ListParseResources;

        /// <summary>
        /// Parses a comma-separated list into an array of conditions.
        /// </summary>
        static public VariantComparison[] ParseConditionsList(StringSlice inConditionsList)
        {
            CachedListParseResources resources = (s_ListParseResources ?? (s_ListParseResources = new CachedListParseResources()));
            resources.ArgsList.Clear();
            int conditionsCount = inConditionsList.Split(resources.Splitter, StringSplitOptions.RemoveEmptyEntries, resources.ArgsList);
            if (conditionsCount > 0)
            {
                VariantComparison[] comparisons = new VariantComparison[conditionsCount];
                for(int i = 0; i < conditionsCount; ++i)
                {
                    if (!VariantComparison.TryParse(resources.ArgsList[i], out comparisons[i]))
                    {
                        Log.Error("[LeafUtils] Unable to parse condition '{0}'", resources.ArgsList[i]);
                    }
                }

                resources.ArgsList.Clear();
                return comparisons;
            }

            resources.ArgsList.Clear();
            return Array.Empty<VariantComparison>();
        }

        /// <summary>
        /// Parses a comma-separated list into an array of conditions.
        /// </summary>
        static public LeafExpressionGroup CompileExpressionGroup(LeafNode inNode, StringSlice inConditionsList)
        {
            return CompileExpressionGroup(inNode.Package(), inConditionsList);
        }

        /// <summary>
        /// Parses a comma-separated list into an array of conditions.
        /// </summary>
        static public LeafExpressionGroup CompileExpressionGroup(LeafNodePackage inPackage, StringSlice inConditionsList)
        {
            Assert.NotNull(inPackage.m_Compiler, "Cannot compile expression group outside of compilation");
            return inPackage.m_Compiler.CompileExpressionGroup(inConditionsList);
        }

        #endregion // Node Parsing

        #region Lookups

        /// <summary>
        /// Attempts to look up the line with the given code, first using the plugin
        /// and falling back to searching the node's module.
        /// </summary>
        static public bool TryLookupLine(ILeafPlugin inPlugin, StringHash32 inLineCode, LeafNode inLocalNode, out string outLine)
        {
            var module = inLocalNode.Package();
            LeafRuntimeConfiguration config = inPlugin.Configuration;
            bool canFindLine = config != null ? !inPlugin.Configuration.IgnoreModuleLineTable : true;

            if (!inPlugin.TryLookupLine(inLineCode, inLocalNode, out outLine) && canFindLine)
            {
                return module.TryGetLine(inLineCode, out outLine);
            }

            return true;
        }

        /// <summary>
        /// Attempts to look up the line info with the given code, first using the plugin
        /// and falling back to searching the node's module.
        /// </summary>
        static public bool TryLookupLineInfo(ILeafPlugin inPlugin, StringHash32 inLineCode, LeafNode inLocalNode, out LeafLineInfo outLineInfo)
        {
            string lineText;
            
            var module = inLocalNode.Package();
            LeafRuntimeConfiguration config = inPlugin.Configuration;
            bool canFindLine = config != null ? !inPlugin.Configuration.IgnoreModuleLineTable : true;

            if (!inPlugin.TryLookupLine(inLineCode, inLocalNode, out lineText) && canFindLine)
            {
                canFindLine = module.TryGetLine(inLineCode, out lineText);
            }

            string customName = module.GetLineCustomName(inLineCode);
            outLineInfo = new LeafLineInfo(inLineCode, lineText, customName);
            return canFindLine;
        }

        /// <summary>
        /// Attempts to look up the node with the given id, first using the plugin
        /// and falling back to searching the node's module.
        /// </summary>
        static public bool TryLookupNode<TNode>(ILeafPlugin<TNode> inPlugin, StringHash32 inNodeId, TNode inLocalNode, out TNode outNode)
            where TNode : LeafNode
        {
            if (!inPlugin.TryLookupNode(inNodeId, inLocalNode, out outNode))
            {
                var module = inLocalNode.Package();
                
                LeafNode node;
                bool bResult = module.TryGetNode(inNodeId, out node);
                outNode = (TNode) node;
                return bResult;
            }

            return true;
        }

        /// <summary>
        /// Attempts to look up a named object with the given id.
        /// </summary>
        static public bool TryLookupObject(ILeafPlugin inPlugin, StringHash32 inTargetId, LeafThreadState ioThreadState, out object outTarget)
        {
            if (ioThreadState != null)
                return ioThreadState.TryLookupObject(inTargetId, out outTarget);

            return inPlugin.TryLookupObject(inTargetId, ioThreadState, out outTarget);
        }

        #endregion // Lookups

        #region Data Extraction

        internal struct OpcodeMask
        {
            public BitSet256 Mask;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(LeafOpcode inOpcode)
            {
                Mask.Set((int) inOpcode);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly bool IsSet(LeafOpcode inOpcode)
            {
                return Mask.IsSet((int) inOpcode);
            }
        }

        private unsafe struct SimulatedStack
        {
            private const int Capacity = 32;

            public fixed ulong EncodedVariants[Capacity];
            public BitSet32 ConstTracker;
            public int StackCount;

            public void PushConst(Variant inVariant)
            {
                Assert.True(StackCount < Capacity);
                int idx = StackCount++;
                EncodedVariants[idx] = Unsafe.FastReinterpret<Variant, ulong>(inVariant);
                ConstTracker.Set(idx);
            }

            public void PushUncertain(Variant inVariant, bool inbConst)
            {
                Assert.True(StackCount < Capacity);
                int idx = StackCount++;
                EncodedVariants[idx] = Unsafe.FastReinterpret<Variant, ulong>(inVariant);
                ConstTracker.Set(idx, inbConst);
            }

            public void PushNonConst()
            {
                Assert.True(StackCount < Capacity);
                int idx = StackCount++;
                EncodedVariants[idx] = 0UL;
                ConstTracker.Unset(idx);
            }

            public Variant Pop()
            {
                Assert.True(StackCount > 0);
                int idx = --StackCount;
                return Unsafe.FastReinterpret<ulong, Variant>(EncodedVariants[idx]);
            }

            public Variant Pop(out bool outConst)
            {
                Assert.True(StackCount > 0);
                int idx = --StackCount;
                outConst = ConstTracker.IsSet(idx);
                return Unsafe.FastReinterpret<ulong, Variant>(EncodedVariants[idx]);
            }
        }

        static private readonly OpcodeMask IndirectOrConditionalMask;

        /// <summary>
        /// Retrieves all referenced line codes in the given node.
        /// </summary>
        static public int ReadAllLineCodes(LeafNode inNode, ICollection<StringHash32> outLineCodes)
        {
            Assert.NotNull(inNode);
            Assert.NotNull(outLineCodes);

            LeafNode node = inNode;
            uint pc = inNode.m_InstructionOffset;
            uint end = pc + node.m_InstructionCount;
            byte[] stream;

            int count = 0;

            LeafOpcode op;
            stream = node.Package().m_Instructions.InstructionStream;
            while (pc < end)
            {
                op = LeafInstruction.ReadOpcode(stream, ref pc);
                switch (op)
                {
                    case LeafOpcode.RunLine:
                    {
                        outLineCodes.Add(LeafInstruction.ReadStringHash32(stream, ref pc));
                        count++;
                        break;
                    }
                    case LeafOpcode.AddChoiceOption:
                    {
                        outLineCodes.Add(LeafInstruction.ReadStringHash32(stream, ref pc));
                        LeafInstruction.ReadByte(stream, ref pc);
                        count++;
                        break;
                    }
                    default:
                    {
                        pc = pc + LeafRuntime.OpSize(op) - 1;
                        break;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Returns if any text is presented in the given node.
        /// </summary>
        static public bool HasTextContent(LeafNode inNode)
        {
            return HasAnyOpcode(inNode, LeafOpcode.RunLine, LeafOpcode.ShowChoices);
        }

        /// <summary>
        /// Returns if there are any indirect or conditional branches in the given node.
        /// </summary>
        static public bool HasIndirectOrConditionalBranching(LeafNode inNode)
        {
            return HasAnyOpcode(inNode, IndirectOrConditionalMask);
        }

        /// <summary>
        /// Retrieves all referenced node identifiers in the given node.
        /// NOTE: this requires simulating all stack operations - don't call this in a hot loop.
        /// </summary>
        static public unsafe int ReadAllDirectlyReferencedNodes(LeafNode inNode, ICollection<StringHash32> outNodeIds)
        {
            Assert.NotNull(inNode);
            Assert.NotNull(outNodeIds);

            LeafNode node = inNode;
            LeafInstructionBlock block = node.Package().m_Instructions;
            uint pc = inNode.m_InstructionOffset;
            uint end = pc + node.m_InstructionCount;
            byte[] stream;

            SimulatedStack stack = default;
            LeafChoice.OptionFlags lastOptionFlags = default;

            int count = 0;

            LeafOpcode op;
            
            stream = block.InstructionStream;
            while (pc < end)
            {
                op = LeafInstruction.ReadOpcode(stream, ref pc);
                switch (op)
                {
                    case LeafOpcode.EvaluateSingleExpression:
                    {
                        LeafInstruction.ReadUInt32(stream, ref pc);
                        stack.PushNonConst();
                        break;
                    }

                    case LeafOpcode.EvaluateExpressionsAnd:
                    case LeafOpcode.EvaluateExpressionsOr:
                    {
                        LeafInstruction.ReadUInt32(stream, ref pc);
                        LeafInstruction.ReadUInt16(stream, ref pc);
                        stack.PushNonConst();
                        break;
                    }

                    case LeafOpcode.InvokeWithReturn_Unoptimized:
                    {
                        LeafInstruction.ReadStringHash32(stream, ref pc);
                        LeafInstruction.ReadStringTableString(stream, ref pc, block.StringTable);
                        stack.PushNonConst();
                        break;
                    }

                    case LeafOpcode.InvokeWithTarget_Unoptimized:
                    {
                        LeafInstruction.ReadStringHash32(stream, ref pc);
                        LeafInstruction.ReadStringTableString(stream, ref pc, block.StringTable);
                        stack.Pop();
                        break;
                    }

                    case LeafOpcode.PushValue:
                    {
                        stack.PushConst(LeafInstruction.ReadVariant(stream, ref pc));
                        break;
                    }

                    case LeafOpcode.PopValue:
                    {
                        stack.Pop();
                        break;
                    }

                    case LeafOpcode.DuplicateValue:
                    {
                        Variant var = stack.Pop(out bool isConst);
                        stack.PushUncertain(var, isConst);
                        stack.PushUncertain(var, isConst);
                        break;
                    }

                    case LeafOpcode.LoadTableValue:
                    {
                        LeafInstruction.ReadTableKeyPair(stream, ref pc);
                        stack.PushNonConst();
                        break;
                    }

                    case LeafOpcode.StoreTableValue:
                    {
                        LeafInstruction.ReadTableKeyPair(stream, ref pc);
                        stack.Pop();
                        break;
                    }

                    case LeafOpcode.Add:
                    case LeafOpcode.Subtract:
                    case LeafOpcode.Multiply:
                    case LeafOpcode.Divide:
                    case LeafOpcode.LessThan:
                    case LeafOpcode.LessThanOrEqualTo:
                    case LeafOpcode.EqualTo:
                    case LeafOpcode.NotEqualTo:
                    case LeafOpcode.GreaterThanOrEqualTo:
                    case LeafOpcode.GreaterThan:
                    {
                        stack.Pop(out bool a);
                        stack.Pop(out bool b);
                        stack.PushUncertain(default, a && b);
                        break;
                    }

                    case LeafOpcode.Not:
                    {
                        Variant var = stack.Pop(out bool a);
                        stack.PushUncertain(!var.AsBool(), a);
                        break;
                    }

                    case LeafOpcode.CastToBool:
                    {
                        Variant var = stack.Pop(out bool a);
                        stack.PushUncertain(var.AsBool(), a);
                        break;
                    }

                    case LeafOpcode.JumpIfFalse:
                    {
                        LeafInstruction.ReadInt16(stream, ref pc);
                        stack.Pop();
                        break;
                    }

                    case LeafOpcode.JumpIndirect:
                    {
                        stack.Pop();
                        break;
                    }

                    case LeafOpcode.GotoNode:
                    case LeafOpcode.BranchNode:
                    case LeafOpcode.ForkNode:
                    case LeafOpcode.ForkNodeUntracked:
                    {
                        outNodeIds.Add(LeafInstruction.ReadStringHash32(stream, ref pc));
                        count++;
                        break;
                    }

                    case LeafOpcode.GotoNodeIndirect:
                    case LeafOpcode.BranchNodeIndirect:
                    case LeafOpcode.ForkNodeIndirect:
                    case LeafOpcode.ForkNodeIndirectUntracked:
                    {
                        Variant v = stack.Pop(out bool isConst);
                        if (isConst)
                        {
                            outNodeIds.Add(v.AsStringHash());
                            count++;
                        }
                        break;
                    }

                    case LeafOpcode.AddChoiceOption:
                    {
                        LeafInstruction.ReadStringHash32(stream, ref pc);
                        byte flags = LeafInstruction.ReadByte(stream, ref pc);
                        lastOptionFlags = (LeafChoice.OptionFlags) flags;

                        stack.Pop();
                        Variant target = stack.Pop(out bool isConst);
                        if (isConst && (lastOptionFlags & LeafChoice.OptionFlags.IsSelector) == 0)
                        {
                            outNodeIds.Add(target.AsStringHash());
                            count++;
                        }
                        
                        break;
                    }

                    case LeafOpcode.AddChoiceAnswer:
                    {
                        LeafInstruction.ReadStringHash32(stream, ref pc);

                        Variant target = stack.Pop(out bool isConst);
                        if (isConst)
                        {
                            outNodeIds.Add(target.AsStringHash());
                            count++;
                        }

                        break;
                    }

                    case LeafOpcode.AddChoiceData:
                    {
                        LeafInstruction.ReadStringHash32(stream, ref pc);
                        stack.Pop();
                        break;
                    }

                    case LeafOpcode.ShowChoices:
                    {
                        stack.PushNonConst();
                        break;
                    }

                    case LeafOpcode.NoOp:
                    {
                        break;
                    }

                    case LeafOpcode.WaitDurationIndirect:
                    {
                        stack.Pop();
                        break;
                    }

                    default:
                    {
                        pc = pc + LeafRuntime.OpSize(op) - 1;
                        break;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Returns if the given opcode is present.
        /// </summary>
        static internal bool HasOpcode(LeafNode inNode, LeafOpcode inOpcode)
        {
            Assert.NotNull(inNode);

            LeafNode node = inNode;
            uint pc = inNode.m_InstructionOffset;
            uint end = pc + node.m_InstructionCount;
            byte[] stream;

            LeafOpcode op;
            stream = node.Package().m_Instructions.InstructionStream;
            while (pc < end)
            {
                op = LeafInstruction.ReadOpcode(stream, ref pc);
                if (op == inOpcode)
                {
                    return true;
                }
                
                pc = pc + LeafRuntime.OpSize(op) - 1;
            }

            return false;
        }

        /// <summary>
        /// Returns if either of the given opcodes are present.
        /// </summary>
        static internal bool HasAnyOpcode(LeafNode inNode, LeafOpcode inOpcode0, LeafOpcode inOpcode1)
        {
            Assert.NotNull(inNode);

            LeafNode node = inNode;
            uint pc = inNode.m_InstructionOffset;
            uint end = pc + node.m_InstructionCount;
            byte[] stream;

            LeafOpcode op;
            stream = node.Package().m_Instructions.InstructionStream;
            while (pc < end)
            {
                op = LeafInstruction.ReadOpcode(stream, ref pc);
                if (op == inOpcode0 || op == inOpcode1)
                {
                    return true;
                }

                pc = pc + LeafRuntime.OpSize(op) - 1;
            }

            return false;
        }

        /// <summary>
        /// Returns if any of the given opcodes are present.
        /// </summary>
        static internal bool HasAnyOpcode(LeafNode inNode, in OpcodeMask inMask)
        {
            Assert.NotNull(inNode);

            LeafNode node = inNode;
            uint pc = inNode.m_InstructionOffset;
            uint end = pc + node.m_InstructionCount;
            byte[] stream;

            LeafOpcode op;
            stream = node.Package().m_Instructions.InstructionStream;
            while (pc < end)
            {
                op = LeafInstruction.ReadOpcode(stream, ref pc);
                if (inMask.IsSet(op))
                {
                    return true;
                }

                pc = pc + LeafRuntime.OpSize(op) - 1;
            }

            return false;
        }

        /// <summary>
        /// Returns if any opcodes in the given range are present.
        /// </summary>
        static internal bool HasAnyOpcodeInRange(LeafNode inNode, LeafOpcode inOpcodeMin, LeafOpcode inOpcodeMax)
        {
            Assert.NotNull(inNode);

            LeafNode node = inNode;
            uint pc = inNode.m_InstructionOffset;
            uint end = pc + node.m_InstructionCount;
            byte[] stream;

            LeafOpcode op;
            stream = node.Package().m_Instructions.InstructionStream;
            while (pc < end)
            {
                op = LeafInstruction.ReadOpcode(stream, ref pc);
                if (op >= inOpcodeMin && op <= inOpcodeMax)
                {
                    return true;
                }

                pc = pc + LeafRuntime.OpSize(op) - 1;
            }

            return false;
        }

        #endregion // Data Extraction

        #region Init

        static LeafUtils()
        {
            OpcodeMask mask = default;
            mask.Add(LeafOpcode.BranchNodeIndirect);
            mask.Add(LeafOpcode.ForkNodeIndirect);
            mask.Add(LeafOpcode.ForkNodeIndirectUntracked);
            mask.Add(LeafOpcode.GotoNodeIndirect);
            mask.Add(LeafOpcode.JumpIndirect);
            mask.Add(LeafOpcode.JumpIfFalse);
            IndirectOrConditionalMask = mask;
        }

        #endregion // Init
    }
}