
using SVO.Runtime.Attributes;
using UnityEngine;

namespace SVO.Runtime
{
    public class PropertyDome : MonoBehaviour
    {
        [Header("像 BoxCollider 一样可编辑的包围盒")]
        [EditableBounds]
        public Bounds area = new Bounds(Vector3.zero, new Vector3(2, 1, 3));
    }
}