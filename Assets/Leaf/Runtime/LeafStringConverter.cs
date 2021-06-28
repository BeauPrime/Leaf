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
            if (ActorType.IsAssignableFrom(inType))
            {
                LeafThreadState thread = (LeafThreadState) inContext;
                thread.TryLookupObject(inData, out outObject);
                return true;
            }

            return StringParser.TryConvertTo(inData, inType, out outObject);
        }
    }
}