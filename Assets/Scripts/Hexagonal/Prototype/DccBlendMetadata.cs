using System;
using UnityEngine;

namespace HexagonalGrids.Prototype
{
    [Serializable] public sealed class DccBlendBinding { public string uid; public Mesh mesh; }
    /// <summary>Persistent import metadata. Gameplay consumes the compiled tileset.</summary>
    [AddComponentMenu("")]
    public sealed class DccBlendMetadata : MonoBehaviour
    {
        [HideInInspector] public string manifestJson, importError;
        [HideInInspector] public DccBlendBinding[] bindings=Array.Empty<DccBlendBinding>();
    }
}
