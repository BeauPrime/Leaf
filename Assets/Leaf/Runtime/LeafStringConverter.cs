/*
 * Copyright (C) 2020. Autumn Beauchesne. All rights reserved.
 * Author:  Autumn Beauchesne
 * Date:    27 June 2021
 * 
 * File:    LeafStringConverter.cs
 * Purpose: Leaf-specific string converter.
 */

using System;
using BeauUtil;
using BeauUtil.Debugger;
using BeauUtil.Variants;

namespace Leaf.Runtime
{
    public class LeafStringConverter : IStringConverter
    {
        static private readonly Type ActorType = typeof(ILeafActor);

        public bool CanConvertTo(Type inType)
        {
            return ActorType.IsAssignableFrom(inType) || StringParser.CanConvertTo(inType);
        }

        public bool TryConvertTo(StringSlice inData, Type inType, object inContext, out object outObject)
        {
            if (TryConvertInline(inData, inType, inContext, out outObject))
                return true;

            if (ActorType.IsAssignableFrom(inType))
            {
                LeafThreadState thread = (LeafThreadState) inContext;
                thread.TryLookupObject(inData, out outObject);
                return true;
            }

            return StringParser.TryConvertTo(inData, inType, out outObject);
        }

        private bool TryConvertInline(StringSlice inData, Type inType, object inContext, out object outObject)
        {
            if (inData.StartsWith("$(") && inData.EndsWith(")"))
            {
                StringSlice inner = inData.Substring(2, inData.Length - 3);;
                VariantOperand operand;
                if (!VariantOperand.TryParse(inner, out operand))
                {
                    Log.Error("[LeafStringConverter] Unable to convert inline operand `{0}` to an operand", inner);
                    outObject = null;
                    return false;
                }

                LeafThreadState thread = (LeafThreadState) inContext;
                Variant returnVal;
                if (!thread.TryResolveOperand(operand, out returnVal))
                {
                    Log.Error("[LeafStringConverter] Unable to resolve inline operand '{0}'", operand);
                    outObject = null;
                    return false;
                }

                if (ActorType.IsAssignableFrom(inType))
                {
                    thread.TryLookupObject(returnVal.AsStringHash(), out outObject);
                    return true;
                }

                return Variant.TryConvertTo(returnVal, inType, out outObject);
            }
            else
            {
                outObject = null;
                return false;
            }
        }
    }
}