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
            /// </summary>
            static public readonly StringHash32 Wait = "_wait";

            /// <summary>
            /// Character event. Will specify a character and, optionally, a pose
            /// </summary>
            static public readonly StringHash32 Character = "_character-id";

            /// <summary>
            /// Pose event. Will specify a pose for the current character.
            /// </summary>
            static public readonly StringHash32 Pose = "_character-pose";
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
            if (!inRoot.IsEmpty)
            {
                ioBuilder.AppendSlice(inRoot);
                if (!inRoot.EndsWith(inSeparator))
                {
                    ioBuilder.Append(inSeparator);
                }
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
        [LeafMember("Wait"), UnityEngine.Scripting.Preserve]
        static public IEnumerator Wait(float inSeconds)
        {
            yield return inSeconds;
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

        #endregion // Default Leaf Members

        #region Default Parsing Configs

        private struct DefaultParseContext
        {
            public readonly ILeafPlugin Plugin;
            public readonly LocalizeDelegate Localize;

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
                    outObject = null;
                    return false;
                }

                Variant returnVal;
                if (!TryResolveVariant(inContext, inner, out returnVal))
                {
                    outObject = null;
                    return false;
                }

                outObject = returnVal;
                return true;
            }
            else
            {
                outObject = null;
                return false;
            }
        }

        /// <summary>
        /// Attempts to handles inline argument syntax.
        /// </summary>
        static public bool TryParseArgument(LeafEvalContext inContext, StringSlice inData, Type inType, out object outObject)
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
                    inContext.Plugin.TryLookupObject(returnVal.AsStringHash(), inContext.Thread, out outObject);
                    return true;
                }

                return Variant.TryConvertTo(returnVal, inType, out outObject);
            }
            else
            {
                return StringParser.TryConvertTo(inData, inType, out outObject);
            }
        }

        /// <summary>
        /// Attempts to handle inline argument syntax.
        /// </summary>
        static public bool TryParseArgument<T>(LeafEvalContext inContext, StringSlice inData, out T outObject)
        {
            object ret;
            bool bSuccess = TryParseArgument(inContext, inData, typeof(T), out ret);
            if (bSuccess)
            {
                outObject = (T) ret;
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
                outValue = null;
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
                        IVariantTable table = inContext.Table;
                        if (table != null && (keyPair.TableId.IsEmpty || keyPair.TableId == table.Name))
                        {
                            bFound = table.TryLookup(keyPair.VariableId, out value);
                        }

                        if (!bFound)
                        {
                            bFound = inContext.Resolver.TryGetVariant(inContext, keyPair, out value);
                        }
                        break;
                    }

                case VariantOperand.Mode.Method:
                    {
                        object rawObj;
                        if (!inContext.MethodCache.TryStaticInvoke(operand.MethodCall, inContext, out rawObj))
                        {
                            Log.Error("[LeafUtils] Unable to execute {0} in inline method call '{1}'", operand.MethodCall, inSource);
                            outValue = null;
                            return false;
                        }

                        if (!Variant.TryConvertFrom(rawObj, out value))
                        {
                            Log.Error("[LeafUtils] Unable to convert result of {0} ({1}) to Variant in inline method call '{2}'", operand.MethodCall, rawObj, inSource);
                            outValue = null;
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
            if (!inPlugin.TryLookupLine(inLineCode, inLocalNode, out outLine))
            {
                var module = inLocalNode.Package();
                return module.TryGetLine(inLineCode, out outLine);
            }

            return true;
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
    }
}