using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SDFTex3DEditorWindow : EditorWindow
{
    [Serializable]
    private enum SourceType
    {
        MeshAsset,
        SceneObject
    }

    [Serializable]
    private enum Axis
    {
        X,
        Y,
        Z
    }

    [Serializable]
    private enum SaveFormat
    {
        RGBAFloat, // 兼容性最好（用 Color 通道保存，r=距离）
        RFloat,    // 单通道浮点
        RHalf,     // 半精度单通道
        Alpha8     // 8bit 归一化（体积较小，精度低）
    }

    private SourceType sourceType = SourceType.SceneObject;
    private Mesh meshAsset;
    private GameObject sceneObject;

    private Vector3Int resolution = new Vector3Int(96, 96, 96);
    private float paddingPercent = 5f;     // 基于包围盒百分比
    private float surfaceThickness = 1.0f; // 相当于体素对角线的倍数
    private int dilateIterations = 0;      // 对表面体素的膨胀迭代数，修补细缝
    private bool invertSign = false;       // 反转 SDF 符号
    private SaveFormat saveFormat = SaveFormat.RGBAFloat;

    private bool autoRebakeOnParamChange = false;

    // 预览
    private Axis previewAxis = Axis.Z;
    private int previewSlice = 0;
    private float previewRangeWorld = 0.1f; // 颜色映射的±范围（世界单位）
    private Texture2D previewTex;
    private Bounds lastBounds;
    private float[] lastSDF; // 按 (z,y,x) 排列
    private Vector3Int lastRes;
    private bool lastValid;

    // 存储贴图
    private Texture3D bakedTexture;

    [MenuItem("Tools/SDF Tex3D Baker")]
    public static void Open()
    {
        GetWindow<SDFTex3DEditorWindow>("SDF Tex3D Baker");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("SDF Tex3D 贴图烘焙器", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        using (new EditorGUILayout.VerticalScope("box"))
        {
            sourceType = (SourceType)EditorGUILayout.EnumPopup("来源类型", sourceType);
            if (sourceType == SourceType.MeshAsset)
            {
                meshAsset = (Mesh)EditorGUILayout.ObjectField("Mesh 资源", meshAsset, typeof(Mesh), false);
            }
            else
            {
                sceneObject = (GameObject)EditorGUILayout.ObjectField("场景对象", sceneObject, typeof(GameObject), true);
            }

            resolution = EditorGUILayout.Vector3IntField("分辨率 (X,Y,Z)", resolution);
            resolution.x = Mathf.Max(8, resolution.x);
            resolution.y = Mathf.Max(8, resolution.y);
            resolution.z = Mathf.Max(8, resolution.z);

            paddingPercent = EditorGUILayout.Slider("包围盒 Padding (%)", paddingPercent, 0f, 20f);
            surfaceThickness = EditorGUILayout.Slider("表面厚度（体素对角倍数）", surfaceThickness, 0.5f, 3f);
            dilateIterations = EditorGUILayout.IntSlider("表面膨胀迭代", dilateIterations, 0, 4);
            invertSign = EditorGUILayout.Toggle("反转 SDF 符号", invertSign);
            saveFormat = (SaveFormat)EditorGUILayout.EnumPopup("保存格式", saveFormat);

            autoRebakeOnParamChange = EditorGUILayout.Toggle("参数变更自动重烘焙", autoRebakeOnParamChange);
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("烘焙 SDF"))
            {
                BakeNow();
            }

            EditorGUI.BeginDisabledGroup(!lastValid);
            if (GUILayout.Button("保存为 .asset"))
            {
                SaveTextureAsset();
            }

            EditorGUI.EndDisabledGroup();
        }

        EditorGUILayout.Space();

        if (lastValid)
        {
            DrawPreview();
        }
    }

    private void OnValidate()
    {
        if (autoRebakeOnParamChange && focusedWindow == this)
        {
            BakeNow();
        }
    }

    private Mesh GetSourceMeshAndMatrix(out Matrix4x4 localToWorld)
    {
        localToWorld = Matrix4x4.identity;

        if (sourceType == SourceType.MeshAsset)
        {
            if (meshAsset == null) return null;
            return meshAsset;
        }

        if (sceneObject == null) return null;

        // 优先 SkinnedMesh 当前姿态
        var smr = sceneObject.GetComponent<SkinnedMeshRenderer>();
        if (smr != null)
        {
            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            localToWorld = sceneObject.transform.localToWorldMatrix;
            return baked;
        }

        var mf = sceneObject.GetComponent<MeshFilter>();
        var mr = sceneObject.GetComponent<MeshRenderer>();
        if (mf != null && mf.sharedMesh != null && mr != null)
        {
            localToWorld = sceneObject.transform.localToWorldMatrix;
            return mf.sharedMesh;
        }

        return null;
    }

    private void BakeNow()
    {
        var mesh = GetSourceMeshAndMatrix(out var l2w);
        if (mesh == null)
        {
            EditorUtility.DisplayDialog("SDF 烘焙", "未找到可用的 Mesh。请指定 Mesh 资源或包含 Mesh 的场景对象。", "确定");
            return;
        }

        var pad = Mathf.Max(0f, paddingPercent) * 0.01f;
        try
        {
            EditorUtility.DisplayProgressBar("SDF 烘焙", "体素化与距离变换中...", 0.1f);
            float[] sdf = SDFBaker3D.Bake(mesh, l2w, resolution, pad, out Bounds usedBounds, surfaceThickness, dilateIterations, invertSign);

            lastSDF = sdf;
            lastBounds = usedBounds;
            lastRes = resolution;
            lastValid = true;
            EnsurePreviewTexture();
            UpdatePreviewTexture();

            // 同步创建 Texture3D（默认 RGBAFloat）
            bakedTexture = CreateTexture3DFromSDF(sdf, usedBounds, resolution, saveFormat);

            EditorUtility.ClearProgressBar();
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("SDF 烘焙失败", ex.Message, "确定");
        }
    }

    private Texture3D CreateTexture3DFromSDF(float[] sdf, Bounds bounds, Vector3Int res, SaveFormat fmt)
    {
        // 距离归一化（可根据包围盒对角的比例进行裁剪）
        float maxDist = 0.5f * bounds.size.magnitude; // 预设范围
        int count = res.x * res.y * res.z;

        switch (fmt)
        {
            case SaveFormat.RFloat:
            {
                var tex = new Texture3D(res.x, res.y, res.z, TextureFormat.RFloat, false);
                var colors = new Color[count];
                for (int i = 0; i < count; i++)
                {
                    colors[i] = new Color(sdf[i], 0, 0, 1);
                }

                tex.SetPixels(colors);
                tex.Apply(false, false);
                return tex;
            }
            case SaveFormat.RHalf:
            {
                var tex = new Texture3D(res.x, res.y, res.z, TextureFormat.RHalf, false);
                var colors = new Color[count];
                for (int i = 0; i < count; i++)
                {
                    colors[i] = new Color(sdf[i], 0, 0, 1);
                }

                tex.SetPixels(colors);
                tex.Apply(false, false);
                return tex;
            }
            case SaveFormat.Alpha8:
            {
                // 映射到 [0,1] 后存 Alpha8
                var tex = new Texture3D(res.x, res.y, res.z, TextureFormat.Alpha8, false);
                var cols = new Color[count];
                for (int i = 0; i < count; i++)
                {
                    float n = 0.5f + 0.5f * Mathf.Clamp(sdf[i] / maxDist, -1f, 1f);
                    cols[i] = new Color(0, 0, 0, n);
                }

                tex.SetPixels(cols);
                tex.Apply(false, false);
                return tex;
            }
            case SaveFormat.RGBAFloat:
            default:
            {
                var tex = new Texture3D(res.x, res.y, res.z, TextureFormat.RGBAFloat, false);
                var colors = new Color[count];
                for (int i = 0; i < count; i++)
                {
                    // r 通道存原始距离（世界单位），g/b 未用
                    colors[i] = new Color(sdf[i], 0, 0, 1);
                }

                tex.SetPixels(colors);
                tex.Apply(false, false);
                return tex;
            }
        }
    }

    private void SaveTextureAsset()
    {
        if (!lastValid || bakedTexture == null)
        {
            EditorUtility.DisplayDialog("保存失败", "还没有可保存的烘焙结果。", "确定");
            return;
        }

        string defaultName = $"SDF_{(sourceType == SourceType.MeshAsset ? (meshAsset != null ? meshAsset.name : "Mesh") : (sceneObject != null ? sceneObject.name : "Object"))}_{lastRes.x}x{lastRes.y}x{lastRes.z}.asset";
        string path = EditorUtility.SaveFilePanelInProject("保存 Texture3D 资源", defaultName, "asset", "选择保存位置");
        if (string.IsNullOrEmpty(path)) return;

        var dup = Instantiate(bakedTexture);
        AssetDatabase.CreateAsset(dup, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("保存成功", $"已保存到 {path}", "确定");
    }

    private void EnsurePreviewTexture()
    {
        int w, h;
        GetPreviewSliceSize(out w, out h);
        if (previewTex == null || previewTex.width != w || previewTex.height != h)
        {
            previewTex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            previewTex.wrapMode = TextureWrapMode.Clamp;
            previewTex.filterMode = FilterMode.Point;
        }

        previewSlice = Mathf.Clamp(previewSlice, 0, GetAxisLength(previewAxis) - 1);
    }

    private void GetPreviewSliceSize(out int w, out int h)
    {
        switch (previewAxis)
        {
            case Axis.X:
                w = lastRes.z;
                h = lastRes.y;
                break; // (z,y)
            case Axis.Y:
                w = lastRes.x;
                h = lastRes.z;
                break; // (x,z)
            case Axis.Z:
            default:
                w = lastRes.x;
                h = lastRes.y;
                break; // (x,y)
        }
    }

    private int GetAxisLength(Axis a)
    {
        switch (a)
        {
            case Axis.X: return lastRes.x;
            case Axis.Y: return lastRes.y;
            case Axis.Z:
            default: return lastRes.z;
        }
    }

    private void DrawPreview()
    {
        EditorGUILayout.LabelField("预览", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            previewAxis = (Axis)EditorGUILayout.EnumPopup("切片轴", previewAxis);
            int maxSlice = GetAxisLength(previewAxis) - 1;
            previewSlice = EditorGUILayout.IntSlider("切片索引", previewSlice, 0, Mathf.Max(0, maxSlice));
            previewRangeWorld = EditorGUILayout.Slider("颜色范围（世界单位，±）", previewRangeWorld, 0.001f, Mathf.Max(0.01f, lastBounds.size.magnitude));

            EnsurePreviewTexture();
            UpdatePreviewTexture();

            float aspect = (float)previewTex.width / previewTex.height;
            Rect r = GUILayoutUtility.GetAspectRect(aspect);
            EditorGUI.DrawPreviewTexture(r, previewTex, null, ScaleMode.StretchToFill);
        }
    }

    private void UpdatePreviewTexture()
    {
        if (previewTex == null || !lastValid) return;

        int w, h;
        GetPreviewSliceSize(out w, out h);

        var cols = new Color[w * h];

        // 将 SDF 映射为蓝-白-红
        float rng = Mathf.Max(1e-6f, previewRangeWorld);

        // idx = (z*yDim + y)*xDim + x 约定在 SDFBaker3D
        switch (previewAxis)
        {
            case Axis.Z:
            {
                int z = Mathf.Clamp(previewSlice, 0, lastRes.z - 1);
                for (int y = 0; y < lastRes.y; y++)
                {
                    for (int x = 0; x < lastRes.x; x++)
                    {
                        int idx3 = (z * lastRes.y + y) * lastRes.x + x;
                        float d = lastSDF[idx3];
                        cols[y * w + x] = DistanceToColor(d, rng);
                    }
                }

                break;
            }
            case Axis.Y:
            {
                int y = Mathf.Clamp(previewSlice, 0, lastRes.y - 1);
                for (int z = 0; z < lastRes.z; z++)
                {
                    for (int x = 0; x < lastRes.x; x++)
                    {
                        int idx3 = (z * lastRes.y + y) * lastRes.x + x;
                        cols[z * w + x] = DistanceToColor(lastSDF[idx3], rng);
                    }
                }

                break;
            }
            case Axis.X:
            {
                int x = Mathf.Clamp(previewSlice, 0, lastRes.x - 1);
                for (int z = 0; z < lastRes.z; z++)
                {
                    for (int y = 0; y < lastRes.y; y++)
                    {
                        int idx3 = (z * lastRes.y + y) * lastRes.x + x;
                        cols[y * w + z] = DistanceToColor(lastSDF[idx3], rng);
                    }
                }

                break;
            }
        }

        previewTex.SetPixels(cols);
        previewTex.Apply(false, false);
    }

    private Color DistanceToColor(float d, float range)
    {
        // 负：蓝 -> 白，正：白 -> 红，0 为白
        float t = Mathf.Clamp(d / range, -1f, 1f);
        if (t < 0f)
        {
            float k = Mathf.InverseLerp(-1f, 0f, t);
            return Color.Lerp(new Color(0.0f, 0.7f, 1.0f, 1f), Color.white, k);
        }
        else
        {
            float k = Mathf.InverseLerp(0f, 1f, t);
            return Color.Lerp(Color.white, new Color(1.0f, 0.3f, 0.1f, 1f), k);
        }
    }
}