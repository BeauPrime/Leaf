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
        [LeafMember("Wait")]
        static public IEnumerator Wait(float inSeconds)
        {
            yield return inSeconds;
        }

        /// <summary>
        /// Waits for the given number of seconds.
        /// This is in real time and does not account for time scale.
        /// </summary>
        [LeafMember("WaitAbs")]
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
        }

        /// <summary>
        /// Configures default handlers
        /// </summary>
        static public void ConfigureDefaultHandlers(TagStringEventHandler inHandler, ILeafPlugin inPlugin)
        {
            
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

            VariantOperand operand;
            if (!VariantOperand.TryParse(data, out operand))
            {
                Log.Error("[LeafUtils] Unable to parse operand '{0}' to operand", data);
                return GetDisplayedErrorString(inSource);
            }

            Variant value = Variant.Null;
            IVariantTable table = inContext as IVariantTable;
            LeafThreadState thread = inContext as LeafThreadState;
            IVariantResolver resolver = (inContext as IVariantResolver) ?? thread?.Resolver ?? inParseContext.Plugin.Resolver;

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
                        if (table != null && (keyPair.TableId.IsEmpty || keyPair.TableId == table.Name))
                        {
                            bFound = table.TryLookup(keyPair.VariableId, out value);
                        }

                        if (!bFound)
                        {
                            bFound = resolver.TryGetVariant(inContext, keyPair, out value);
                        }
                        break;
                    }

                case VariantOperand.Mode.Method:
                    {
                        object rawObj;
                        if (!inParseContext.Plugin.MethodCache.TryStaticInvoke(operand.MethodCall, inContext, out rawObj))
                        {
                            Log.Error("[LeafUtils] Unable to execute {0} in inline method call '{1}'", operand.MethodCall, inSource);
                            return GetDisplayedErrorString(inSource);
                        }

                        if (!Variant.TryConvertFrom(rawObj, out value))
                        {
                            Log.Error("[LeafUtils] Unable to convert result of {0} ({1}) to Variant in inline method call '{2}'", operand.MethodCall, rawObj, inSource);
                            return GetDisplayedErrorString(inSource);
                        }
                        break;
                    }

                default:
                    throw new IndexOutOfRangeException("Unknown VariantOperand type " + operand.Type);
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
    }
}