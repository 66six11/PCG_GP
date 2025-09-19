
using System.Collections.Generic;
using FieldVisual.Runtime.Attributes;
using UnityEngine;

namespace SVO.Runtime
{
    public class PropertyDome : MonoBehaviour
    {
        [Header("像 BoxCollider 一样可编辑的包围盒")]
        [EditableBounds]
        public Bounds area = new Bounds(Vector3.zero, new Vector3(2, 1, 3));
        [EditableBounds(true)]
        public Bounds area2 = new Bounds(Vector3.zero, new Vector3(2, 1, 3));
        
        [EditableBounds( false, true, false )]
        public Bounds area3 = new Bounds(Vector3.zero, new Vector3(2, 1, 3));
        
        [EditableBounds]
        public   List<Bounds> bounds = new List<Bounds>();
    }
}