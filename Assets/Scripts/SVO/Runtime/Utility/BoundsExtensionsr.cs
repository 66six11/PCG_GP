using UnityEngine;

namespace SVO.Runtime.Utility
{
    public static class BoundsExtensions
    {
        public static bool ContainsBounds(this Bounds container, Bounds containee)
        {
            return
                container.min.x <= containee.min.x &&
                container.min.y <= containee.min.y &&
                container.min.z <= containee.min.z &&
                container.max.x >= containee.max.x &&
                container.max.y >= containee.max.y &&
                container.max.z >= containee.max.z;
        }
        
    }
}