using UnityEngine;

namespace FieldVisual.Runtime.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class EditableBoundsAttribute : PropertyAttribute
    {
        public bool ShowCenterHandle = true;
        public bool ShowSizeHandles = true;
        public bool ShowCornerHandles = true;
        public bool Follow = false;
        public Color HandleColor = new Color(0, 1, 0, 0.5f);

        // 新增字段
        // true: 用“局部空间”存储与编辑；false: 用“世界空间”存储与编辑
        public bool Local = false;

        // 是否跟随物体旋转（仅 Local=true 时生效）
        public bool FollowRotation = false;

        // 是否跟随缩放（仅 Local=true 时生效）
        public bool FollowScale = true;

        public EditableBoundsAttribute()
        {
        }
        
        public EditableBoundsAttribute(
            bool follow,
            bool showSizeHandles = true,
            bool local = true,
            bool followRotation = true,
            bool followScale = true)
        {
            Follow = follow;
            ShowSizeHandles = showSizeHandles;
            Local = local;
            FollowRotation = followRotation;
            FollowScale = followScale;
        }
    }
}