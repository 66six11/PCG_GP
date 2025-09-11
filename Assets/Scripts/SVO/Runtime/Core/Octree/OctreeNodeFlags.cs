using System;

namespace SVO.Runtime.Core
{
    [Flags]
    public enum OctreeNodeFlags : byte
    {
        None = 0,
        Walkable = 1 << 0,
        Blocked = 1 << 1,
        Visited = 1 << 2,
        InOpenSet = 1 << 3,
        InClosedSet = 1 << 4,
        IsPath = 1 << 5
    }
}