using UnityEngine;

namespace SVO.Runtime.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class EditableBoundsAttribute : PropertyAttribute
    {
        public bool ShowCenterHandle = true;
        public bool ShowSizeHandles = true;
        public bool ShowCornerHandles = true;
        public Color HandleColor = new Color(0, 1, 0, 0.5f);
    
        public EditableBoundsAttribute() { }
    
        public EditableBoundsAttribute(bool showCenter, bool showSize, bool showCorners)
        {
            ShowCenterHandle = showCenter;
            ShowSizeHandles = showSize;
            ShowCornerHandles = showCorners;
        }
    
        public EditableBoundsAttribute(float r, float g, float b, float a = 0.5f)
        {
            HandleColor = new Color(r, g, b, a);
        }
    }
}