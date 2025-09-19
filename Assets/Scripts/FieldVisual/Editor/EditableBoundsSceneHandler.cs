#if UNITY_EDITOR


using System;
using System.Reflection;
using FieldVisual.Runtime.Attributes;
using UnityEditor;
using UnityEngine;

namespace FieldVisual.Editor
{
    [InitializeOnLoad]
    public static class EditableBoundsSceneHandler
    {
        static EditableBoundsSceneHandler()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            var go = Selection.activeGameObject;
            if (!go) return;

            foreach (var component in go.GetComponents<Component>())
            {
                if (!component) continue;

                var so = new SerializedObject(component);
                var it = so.GetIterator();
                bool enterChildren = true;

                while (it.NextVisible(enterChildren))
                {
                    enterChildren = false;

                    // 通过反射找到字段（包含私有字段）
                    var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    var field = component.GetType().GetField(it.name, flags);
                    if (field == null) continue;

                    // 仅处理标注了 EditableBoundsAttribute 的字段
                    var attr = field.GetCustomAttribute<EditableBoundsAttribute>(true);
                    if (attr == null) continue;

                    // 1) 单个 Bounds
                    if (it.propertyType == SerializedPropertyType.Bounds)
                    {
                        DrawBoundsFlexible(so, it.Copy(), component.transform, attr, field.Name);
                        continue;
                    }

                    // 2) 数组 / List<Bounds>
                    if (it.isArray && it.propertyType == SerializedPropertyType.Generic)
                    {
                        var elemType = GetElementType(field.FieldType);
                        if (elemType == typeof(Bounds))
                        {
                            var arrayProp = it.Copy();
                            int size = arrayProp.arraySize;
                            for (int i = 0; i < size; i++)
                            {
                                var elem = arrayProp.GetArrayElementAtIndex(i);
                                if (elem.propertyType != SerializedPropertyType.Bounds) continue;
                                DrawBoundsFlexible(so, elem, component.transform, attr, $"{field.Name}[{i}]");
                            }
                        }
                    }
                }
            }
        }

        private static Type GetElementType(Type t)
        {
            if (t.IsArray) return t.GetElementType();
            if (t.IsGenericType)
            {
                var args = t.GetGenericArguments();
                if (args.Length == 1) return args[0];
            }
            return null;
        }

        // 根据 Local/Follow/FollowRotation/FollowScale 选择绘制与编辑策略
        private static void DrawBoundsFlexible(SerializedObject so, SerializedProperty boundsProp, Transform t, EditableBoundsAttribute attr, string label)
        {
            if (attr.Local)
                DrawLocal(so, boundsProp, t, attr, label);
            else
                DrawWorld(so, boundsProp, t, attr, label);
        }

        // Local 模式：boundsProp 保存“局部空间”的 Bounds
        private static void DrawLocal(SerializedObject so, SerializedProperty boundsProp, Transform t, EditableBoundsAttribute attr, string label)
        {
            // 旋转由矩阵控制；缩放我们不直接放入矩阵，而是用“可视缩放”对 center/size 做等比变换，这样易于把交互结果还原回本地值
            var rot = attr.FollowRotation ? t.rotation : Quaternion.identity;
            using (new Handles.DrawingScope(attr.HandleColor, Matrix4x4.TRS(t.position, rot, Vector3.one)))
            {
                var localBounds = boundsProp.boundsValue;

                // Follow：中心固定在本地原点（仅在需要时写回，避免 Undo 噪声）
                if (attr.Follow && localBounds.center != Vector3.zero)
                {
                    Undo.RecordObject(so.targetObject, "Snap Local Bounds Center To Origin");
                    localBounds.center = Vector3.zero;
                    boundsProp.boundsValue = localBounds;
                    so.ApplyModifiedProperties();
                }

                // 可视缩放向量（取绝对值避免负缩放翻转）
                var s = attr.FollowScale ? Abs(t.lossyScale) : Vector3.one;

                // 将本地 center/size 映射到“手柄可视空间”
                Vector3 centerVis = Vector3.Scale(localBounds.center, s);
                Vector3 sizeVis   = Vector3.Scale(localBounds.size,   s);
                Vector3 halfVis   = sizeVis * 0.5f;

                // 绘制线框与标签
                Handles.DrawWireCube(centerVis, sizeVis);
                DrawLabel(centerVis, label);

                // 非 Follow：允许在可视空间拖拽中心，回写时除以缩放还原到本地
                if (!attr.Follow)
                {
                    EditorGUI.BeginChangeCheck();
                    var newCenterVis = Handles.PositionHandle(centerVis, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(so.targetObject, "Move Local Bounds Center");
                        var newCenterLocal = UnscaleSafe(newCenterVis, s);
                        localBounds.center = newCenterLocal;
                        boundsProp.boundsValue = localBounds;
                        so.ApplyModifiedProperties();
                    }
                }

                // 尺寸手柄（可视空间下操作，之后换算回本地）
                if (attr.ShowSizeHandles)
                {
                    bool changed = false;

                    changed |= DrawSizeHandleScaled(centerVis + Vector3.right   * halfVis.x, Vector3.right,  ref halfVis);
                    changed |= DrawSizeHandleScaled(centerVis - Vector3.right   * halfVis.x, Vector3.left,   ref halfVis);
                    changed |= DrawSizeHandleScaled(centerVis + Vector3.up      * halfVis.y, Vector3.up,     ref halfVis);
                    changed |= DrawSizeHandleScaled(centerVis - Vector3.up      * halfVis.y, Vector3.down,   ref halfVis);
                    changed |= DrawSizeHandleScaled(centerVis + Vector3.forward * halfVis.z, Vector3.forward,ref halfVis);
                    changed |= DrawSizeHandleScaled(centerVis - Vector3.forward * halfVis.z, Vector3.back,   ref halfVis);

                    if (changed)
                    {
                        // 还原到本地 extents
                        var halfLocal = UnscaleSafe(halfVis, s);
                        halfLocal.x = Mathf.Max(0.01f, halfLocal.x);
                        halfLocal.y = Mathf.Max(0.01f, halfLocal.y);
                        halfLocal.z = Mathf.Max(0.01f, halfLocal.z);

                        Undo.RecordObject(so.targetObject, "Resize Local Bounds");
                        localBounds.extents = halfLocal;
                        boundsProp.boundsValue = localBounds;
                        so.ApplyModifiedProperties();
                    }
                }
            }
        }

        // World 模式：boundsProp 保存“世界空间”的 Bounds（AABB）
        private static void DrawWorld(SerializedObject so, SerializedProperty boundsProp, Transform t, EditableBoundsAttribute attr, string label)
        {
            using (new Handles.DrawingScope(attr.HandleColor))
            {
                var worldBounds = boundsProp.boundsValue;

                // Follow：同步中心到物体位置（旋转/缩放在世界 AABB 下不处理）
                if (attr.Follow)
                {
                    var pos = t.position;
                    if (worldBounds.center != pos)
                    {
                        Undo.RecordObject(so.targetObject, "Sync World Bounds Center To Transform");
                        worldBounds.center = pos;
                        boundsProp.boundsValue = worldBounds;
                        so.ApplyModifiedProperties();
                    }
                }

                // 绘制（世界轴对齐）
                Handles.DrawWireCube(worldBounds.center, worldBounds.size);
                DrawLabel(worldBounds.center, label);

                // 非 Follow：允许世界中心拖拽
                if (!attr.Follow)
                {
                    EditorGUI.BeginChangeCheck();
                    var newCenter = Handles.PositionHandle(worldBounds.center, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(so.targetObject, "Move World Bounds Center");
                        worldBounds.center = newCenter;
                        boundsProp.boundsValue = worldBounds;
                        so.ApplyModifiedProperties();
                    }
                }

                // 尺寸（世界轴向）
                if (attr.ShowSizeHandles)
                {
                    var half = worldBounds.extents;
                    bool changed = false;

                    changed |= DrawSizeHandle(worldBounds.center + Vector3.right   * half.x, Vector3.right,   ref half);
                    changed |= DrawSizeHandle(worldBounds.center - Vector3.right   * half.x, Vector3.left,    ref half);
                    changed |= DrawSizeHandle(worldBounds.center + Vector3.up      * half.y, Vector3.up,      ref half);
                    changed |= DrawSizeHandle(worldBounds.center - Vector3.up      * half.y, Vector3.down,    ref half);
                    changed |= DrawSizeHandle(worldBounds.center + Vector3.forward * half.z, Vector3.forward, ref half);
                    changed |= DrawSizeHandle(worldBounds.center - Vector3.forward * half.z, Vector3.back,    ref half);

                    if (changed)
                    {
                        half.x = Mathf.Max(0.01f, half.x);
                        half.y = Mathf.Max(0.01f, half.y);
                        half.z = Mathf.Max(0.01f, half.z);

                        Undo.RecordObject(so.targetObject, "Resize World Bounds");
                        worldBounds.extents = half;
                        boundsProp.boundsValue = worldBounds;
                        so.ApplyModifiedProperties();
                    }
                }
            }
        }

        private static void DrawLabel(Vector3 pos, string label)
        {
            if (string.IsNullOrEmpty(label)) return;
            var offset = Vector3.up * (HandleUtility.GetHandleSize(pos) * 0.2f);
            Handles.Label(pos + offset, label, EditorStyles.boldLabel);
        }

        // 可视空间尺寸手柄（当前 Handles.matrix 下），操作 halfVis（半尺寸，已包含 FollowScale 的缩放）
        private static bool DrawSizeHandleScaled(Vector3 pos, Vector3 dir, ref Vector3 halfVis)
        {
            EditorGUI.BeginChangeCheck();
            Handles.color = Color.blue;
            float handleSize = HandleUtility.GetHandleSize(pos) * 0.1f;
            var newPos = Handles.Slider(pos, dir, handleSize, Handles.SphereHandleCap, 0.1f);
            if (EditorGUI.EndChangeCheck())
            {
                var delta = newPos - pos;
                if (dir.x != 0) halfVis.x += delta.x * Mathf.Sign(dir.x);
                if (dir.y != 0) halfVis.y += delta.y * Mathf.Sign(dir.y);
                if (dir.z != 0) halfVis.z += delta.z * Mathf.Sign(dir.z);
                return true;
            }
            return false;
        }

        // 世界/无缩放通用尺寸手柄（half 在当前矩阵空间下）
        private static bool DrawSizeHandle(Vector3 pos, Vector3 dir, ref Vector3 half)
        {
            EditorGUI.BeginChangeCheck();
            Handles.color = Color.blue;
            float handleSize = HandleUtility.GetHandleSize(pos) * 0.1f;
            var newPos = Handles.Slider(pos, dir, handleSize, Handles.SphereHandleCap, 0.1f);
            if (EditorGUI.EndChangeCheck())
            {
                var delta = newPos - pos;
                if (dir.x != 0) half.x += delta.x * Mathf.Sign(dir.x);
                if (dir.y != 0) half.y += delta.y * Mathf.Sign(dir.y);
                if (dir.z != 0) half.z += delta.z * Mathf.Sign(dir.z);
                return true;
            }
            return false;
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        // 将可视空间值除以缩放安全还原（避免 0 缩放导致 NaN）
        private static Vector3 UnscaleSafe(Vector3 v, Vector3 s)
        {
            const float eps = 1e-6f;
            float ix = Mathf.Abs(s.x) > eps ? 1f / s.x : 0f;
            float iy = Mathf.Abs(s.y) > eps ? 1f / s.y : 0f;
            float iz = Mathf.Abs(s.z) > eps ? 1f / s.z : 0f;
            return new Vector3(v.x * ix, v.y * iy, v.z * iz);
        }
    }
}
#endif