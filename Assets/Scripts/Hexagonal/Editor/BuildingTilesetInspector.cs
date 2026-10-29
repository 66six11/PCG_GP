#if UNITY_EDITOR
using System.Linq;
using HexagonalGrids.Prototype;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(BuildingTileset))]
    public sealed class BuildingTilesetInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var tileset=(BuildingTileset)target;
            EditorGUILayout.LabelField("模型集",tileset.tilesetId);
            EditorGUILayout.LabelField("变体",tileset.variants.Length.ToString());
            EditorGUILayout.LabelField("八角状态覆盖",tileset.variants.Select(v=>v.model.stateCode).Distinct().Count()+" / 254");
            EditorGUILayout.LabelField("源描述",tileset.sourceJson);
            foreach(var message in tileset.diagnostics) EditorGUILayout.HelpBox(message,MessageType.Warning);
            bool native=DccBlendImporter.Handles(tileset.sourceJson);
            if(GUILayout.Button(native?"选择源 Blender 工程":"选择源 JSON")) Selection.activeObject=AssetDatabase.LoadMainAssetAtPath(tileset.sourceJson);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if(GUILayout.Button("重新编译")) {
                    try { if(native)BuildingTilesetImporter.CompileBlend(tileset.sourceJson);else BuildingTilesetImporter.Compile(tileset.sourceJson); }
                    catch(System.Exception e) { Debug.LogException(e); }
                }
            EditorGUILayout.HelpBox("在 UnifiedGridBuildingDemo 的 Building Tileset 字段指定此资产。修改模型与连接规则请返回 Blender。",MessageType.Info);
        }
    }
}
#endif
