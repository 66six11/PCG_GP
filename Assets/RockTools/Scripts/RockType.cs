using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RockTools
{
    // -------------------------------------------------------------------------------------------------------------

    public enum ERockType
    {
        Cubic = 0,
        Sharp = 1,
        Crystal = 2,

        Custom = 255,
    }

    // -------------------------------------------------------------------------------------------------------------

    public static class RockTypeExtensions
    {
        public const int RockTypesLenght = 4;

        public static string GetResourcesPath(this ERockType rockType)
        {
            switch (rockType)
            {
                case ERockType.Cubic:
                    return "Meshes/Type01";
                case ERockType.Sharp:
                    return "Meshes/Type02";
                case ERockType.Crystal:
                    return "Meshes/Type03";
                case ERockType.Custom:
                    return "Meshes/Custom";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static LogicBase GetLogicInstance(this ERockType rockType)
        {
            switch (rockType)
            {
                case ERockType.Cubic:
                    return ScriptableObject.CreateInstance<LogicType01>();
                case ERockType.Sharp:
                    return ScriptableObject.CreateInstance<LogicType02>();
                case ERockType.Crystal:
                    return ScriptableObject.CreateInstance<LogicType03>();
                case ERockType.Custom:
                    return ScriptableObject.CreateInstance<LogicTypeCustom>();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static int GetTypeIndex(this ERockType rockType)
        {
            switch (rockType)
            {
                case ERockType.Cubic:
                    return 0;
                case ERockType.Sharp:
                    return 1;
                case ERockType.Crystal:
                    return 2;
                case ERockType.Custom:
                    return 3;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    // -------------------------------------------------------------------------------------------------------------

    public static class EnumUtil
    {
        public static IEnumerable<T> GetValues<T>()
        {
            return Enum.GetValues(typeof(T)).Cast<T>();
        }
    }

    // -------------------------------------------------------------------------------------------------------------
}