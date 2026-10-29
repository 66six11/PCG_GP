#if UNITY_EDITOR
using HexagonalGrids.Prototype;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(TerrainBuildingDemo))]
    public sealed class TerrainBuildingDemoInspector : UnityEditor.Editor
    {
        private void OnEnable() => Undo.undoRedoPerformed += Refresh;
        private void OnDisable() => Undo.undoRedoPerformed -= Refresh;
        private void Refresh()
        {
            var demo = target as TerrainBuildingDemo;
            if (demo != null && demo.isActiveAndEnabled) { demo.RefreshPreview(); SceneView.RepaintAll(); }
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "rockDensity", "rockConformity", "cliffHeight");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rockDensity"), new GUIContent("岩石覆盖量", "0不生成，1为最大候选密度"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rockConformity"), new GUIContent("贴坡程度", "越大越随坡面倾斜，长石仍保留竖向轮廓"));
            if (serializedObject.ApplyModifiedProperties()) Refresh();
            GUILayout.Space(8);
            if (GUILayout.Button("生成陡坡示例"))
            {
                var demo = (TerrainBuildingDemo)target;
                demo.RefreshPreview(); demo.CreateSlopeExample(); SceneView.RepaintAll();
            }
            if (GUILayout.Button("刷新覆盖预览")) Refresh();
        }
    }
}
#endif
