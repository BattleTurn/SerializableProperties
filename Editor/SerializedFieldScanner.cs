using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BattleTurn.Serializable.Editor
{
    /// <summary>
    /// Works out which fields of a type Unity both serializes and shows, in the order it
    /// shows them. Kept separate from the drawer so it can be tested without any UI.
    /// </summary>
    public static class SerializedFieldScanner
    {
        /// <summary>
        /// Base-class fields first, then the type own fields - the order the Inspector uses.
        /// Returns an empty list for primitives, enums and UnityEngine.Object types, which
        /// are drawn as a single value rather than a row of columns.
        /// </summary>
        public static List<FieldInfo> GetColumns(Type type)
        {
            var columns = new List<FieldInfo>();
            if (type == null || !IsRowType(type))
                return columns;

            // Walk to the root first so base fields come out before derived ones.
            var chain = new List<Type>();
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
                chain.Add(t);
            chain.Reverse();

            foreach (Type t in chain)
            {
                var declared = t.GetFields(BindingFlags.Instance | BindingFlags.Public
                                           | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (var f in declared)
                    if (IsSerializedAndVisible(f))
                        columns.Add(f);
            }

            return columns;
        }

        /// <summary>A type whose fields can become columns, rather than a single-value cell.</summary>
        public static bool IsRowType(Type type)
        {
            if (type == null) return false;
            if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return false;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return false;
            // Unity draws these as one field even though they are structs with members.
            if (type.Namespace == "UnityEngine" && type.IsValueType) return false;
            return type.IsValueType || type.IsClass;
        }

        private static bool IsSerializedAndVisible(FieldInfo f)
        {
            if (f.IsStatic || f.IsLiteral || f.IsInitOnly) return false;
            if (f.IsDefined(typeof(NonSerializedAttribute), false)) return false;
            if (f.IsDefined(typeof(HideInInspector), false)) return false;

            // Unity serializes public fields, and private ones only with [SerializeField].
            if (f.IsPublic) return true;
            return f.IsDefined(typeof(SerializeField), false);
        }
    }
}
