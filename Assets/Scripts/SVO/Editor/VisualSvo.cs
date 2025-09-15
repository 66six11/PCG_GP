using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace SVO.Editor
{
    [Overlay(typeof(SceneView), "SVO")]
    public class VisualSvo : Overlay
    {
        private bool _showVisualSvo = false;

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement() { name = "可视化SVO" };
            root.Add(new Label() { text = "查找到的SVO" });
            root.Add(new Toggle() { text = "可见" , value = _showVisualSvo});
            return root;
        }
    }
}