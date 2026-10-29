using System;
using UnityEngine;
using Random = System.Random;

namespace RockTools
{
    [Serializable]
    public class LogicBase : ScriptableObject
    {
        protected virtual ERockType pRockType { get; }

        protected Mesh[] rockMeshes;

        // ----------------------------------------------------------------------------------------

        public virtual void UpdateRock(ref MeshBuffer meshBuffer, Random random)
        {
            PrepareMeshes();
        }

        // ----------------------------------------------------------------------------------------

        protected void PrepareMeshes()
        {
            rockMeshes = Resources.LoadAll<Mesh>(pRockType.GetResourcesPath());
        }

        // ----------------------------------------------------------------------------------------
    }
}