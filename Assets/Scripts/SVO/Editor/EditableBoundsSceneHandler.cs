#if UNITY_EDITOR
using System.Reflection;
using SVO.Runtime.Attributes;
using UnityEditor;
using UnityEngine;

namespace SVO.Editor
{
    [InitializeOnLoad]
    public static class EditableBoundsSceneHandler
    {
        private static EditableBoundsAttribute _currentAttribute;
        private static SerializedObject _serializedObject;
        private static SerializedProperty _boundsProperty;
        private static FieldInfo fieldInfo;
        static EditableBoundsSceneHandler()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }
    
        private static void OnSceneGUI(SceneView sceneView)
        {
            // 只处理选中的对象
            if (Selection.activeGameObject == null) return;
        
            // 检查所有组件
            foreach (Component component in Selection.activeGameObject.GetComponents<Component>())
            {
                if (component == null) continue;
            
                SerializedObject so = new SerializedObject(component);
                SerializedProperty prop = so.GetIterator();
            
                while (prop.NextVisible(true))
                {
                    if (prop.propertyType != SerializedPropertyType.Bounds) continue;
               
                    // 检查是否有 EditableBoundsAttribute
                    //反射prop的属性
                    fieldInfo = component.GetType().GetField(prop.name);
                    object[] attributes = fieldInfo.GetCustomAttributes(typeof(EditableBoundsAttribute), true);
                    if (attributes.Length == 0) continue;
                
                    _currentAttribute = attributes[0] as EditableBoundsAttribute;
                    _serializedObject = so;
                    _boundsProperty = prop.Copy();
                
                    // 绘制编辑手柄
                    DrawBoundsHandles(prop.boundsValue);
                    return; // 只处理第一个找到的
                }
            }
        }
    
        private static void DrawBoundsHandles(Bounds bounds)
        {
            Handles.color = _currentAttribute.HandleColor;
        
            // 绘制边界框
            Handles.DrawWireCube(bounds.center, bounds.size);
        
            // 绘制中心点手柄
            if (_currentAttribute.ShowCenterHandle)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newCenter = Handles.PositionHandle(bounds.center, Quaternion.identity);
            
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_serializedObject.targetObject, "Move Bounds Center");
                    Bounds newBounds = new Bounds(newCenter, bounds.size);
                    _boundsProperty.boundsValue = newBounds;
                    _serializedObject.ApplyModifiedProperties();
                }
            }
        
            // 绘制尺寸手柄
            if (_currentAttribute.ShowSizeHandles)
            {
                Vector3 halfSize = bounds.extents;
            
                // X 轴方向
                DrawSizeHandle(bounds.center + Vector3.right * halfSize.x, Vector3.right, halfSize);
                DrawSizeHandle(bounds.center - Vector3.right * halfSize.x, Vector3.left, halfSize);
            
                // Y 轴方向
                DrawSizeHandle(bounds.center + Vector3.up * halfSize.y, Vector3.up, halfSize);
                DrawSizeHandle(bounds.center - Vector3.up * halfSize.y, Vector3.down, halfSize);
            
                // Z 轴方向
                DrawSizeHandle(bounds.center + Vector3.forward * halfSize.z, Vector3.forward, halfSize);
                DrawSizeHandle(bounds.center - Vector3.forward * halfSize.z, Vector3.back, halfSize);
            }
        
            // 绘制角点手柄
            if (_currentAttribute.ShowCornerHandles)
            {
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 corner = bounds.center + new Vector3(
                                bounds.extents.x * x,
                                bounds.extents.y * y,
                                bounds.extents.z * z
                            );
                        
                            DrawCornerHandle(corner, bounds);
                        }
                    }
                }
            }
        }
    
        private static void DrawSizeHandle(Vector3 position, Vector3 direction, Vector3 halfSize)
        {
            EditorGUI.BeginChangeCheck();
        
            Handles.color = Color.blue;
            float handleSize = HandleUtility.GetHandleSize(position) * 0.1f;
            Vector3 newPosition = Handles.Slider(position, direction, handleSize, Handles.SphereHandleCap, 0.1f);
        
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_serializedObject.targetObject, "Resize Bounds");
            
                Vector3 delta = newPosition - position;
                Vector3 newHalfSize = halfSize;
            
                if (direction.x != 0) newHalfSize.x += delta.x * Mathf.Sign(direction.x);
                if (direction.y != 0) newHalfSize.y += delta.y * Mathf.Sign(direction.y);
                if (direction.z != 0) newHalfSize.z += delta.z * Mathf.Sign(direction.z);
            
                // 确保大小不为负
                newHalfSize.x = Mathf.Max(0.01f, newHalfSize.x);
                newHalfSize.y = Mathf.Max(0.01f, newHalfSize.y);
                newHalfSize.z = Mathf.Max(0.01f, newHalfSize.z);
            
                Bounds newBounds = new Bounds(_boundsProperty.boundsValue.center, newHalfSize * 2);
                _boundsProperty.boundsValue = newBounds;
                _serializedObject.ApplyModifiedProperties();
            }
        }
    
        private static void DrawCornerHandle(Vector3 position, Bounds bounds)
        {
            EditorGUI.BeginChangeCheck();
        
            Handles.color = Color.red;
            float handleSize = HandleUtility.GetHandleSize(position) * 0.1f;
            var fmh_152_13_638935702552842438 = Quaternion.identity; Vector3 newPosition = Handles.FreeMoveHandle(
                position,
                handleSize,
                Vector3.zero,
                Handles.SphereHandleCap
            );
        
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_serializedObject.targetObject, "Resize Bounds Corner");
            
                Vector3 delta = newPosition - position;
                Vector3 newHalfSize = bounds.extents;
            
                // 根据角点位置调整大小
                newHalfSize.x = Mathf.Max(0.01f, Mathf.Abs(position.x - bounds.center.x) + Mathf.Abs(delta.x));
                newHalfSize.y = Mathf.Max(0.01f, Mathf.Abs(position.y - bounds.center.y) + Mathf.Abs(delta.y));
                newHalfSize.z = Mathf.Max(0.01f, Mathf.Abs(position.z - bounds.center.z) + Mathf.Abs(delta.z));
            
                Bounds newBounds = new Bounds(bounds.center, newHalfSize * 2);
                _boundsProperty.boundsValue = newBounds;
                _serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
#endif