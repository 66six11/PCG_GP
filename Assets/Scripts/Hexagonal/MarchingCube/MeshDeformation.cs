using Job;
using Unity.Collections;
using UnityEngine;

namespace MarchingCube
{
    /// <summary>Owns one reusable output mesh and vertex buffer for this renderer.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter))]
    public sealed class MeshDeformation : MonoBehaviour
    {
        private MeshFilter filter;
        private Mesh output;
        private Mesh source;
        private object sourceRevision;
        private bool outputMirrored;
        private NativeArray<Vector3> vertices;
        private MeshDeformationTemplate localTemplate;
        private MeshDeformationTemplate appliedTemplate;
        private Vector3 lastA, lastB, lastC, lastD;
        private float lastHeight;
        private bool hasApplied;
        public Mesh Mesh => output;

        private void OnEnable()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseResources;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseResources;
#endif
        }

        public Mesh Apply(Mesh template, Quaternion rotation, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float height, bool mirrorX = false)
        {
            if (localTemplate == null || !localTemplate.IsCreated || localTemplate.Source != template
                || !localTemplate.Rotation.Equals(rotation) || localTemplate.MirrorX != mirrorX)
            {
                var next = new MeshDeformationTemplate(template, rotation, mirrorX: mirrorX);
                localTemplate?.Dispose();
                localTemplate = next;
            }
            return Apply(localTemplate, a, b, c, d, height);
        }

        internal Mesh Apply(MeshDeformationTemplate template, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float height)
        {
            if (filter == null) filter = GetComponent<MeshFilter>();
            if (output == null || source != template.Source || !ReferenceEquals(sourceRevision, template.SourceRevision) || outputMirrored != template.MirrorX)
            {
                var next = Instantiate(template.Source);
                next.name = template.Source.name + " (Deformed)";
                next.hideFlags = HideFlags.DontSave;
                next.MarkDynamic();
                if (template.MirrorX) MeshModifyJob.ReverseWinding(next);
                if (vertices.IsCreated) vertices.Dispose();
                vertices = new NativeArray<Vector3>(template.Source.vertexCount, Allocator.Persistent);
                MeshModifyJob.DestroyMesh(output);
                output = next;
                source = template.Source;
                sourceRevision = template.SourceRevision;
                outputMirrored = template.MirrorX;
                hasApplied = false;
            }
            if (!hasApplied || !ReferenceEquals(appliedTemplate, template) || !lastA.Equals(a)
                || !lastB.Equals(b) || !lastC.Equals(c) || !lastD.Equals(d) || lastHeight != height)
            {
                MeshModifyJob.TransformInto(template, vertices, output, a, b, c, d, height);
                appliedTemplate = template;
                lastA = a; lastB = b; lastC = c; lastD = d; lastHeight = height;
                hasApplied = true;
            }
            filter.sharedMesh = output;
            return output;
        }

        public void Hide()
        {
            if (filter == null) filter = GetComponent<MeshFilter>();
            filter.sharedMesh = null;
            hasApplied = false;
        }

        public void ReleaseResources()
        {
            if (filter != null && filter.sharedMesh == output) filter.sharedMesh = null;
            MeshModifyJob.DestroyMesh(output);
            output = null;
            source = null;
            sourceRevision = null;
            hasApplied = false;
            appliedTemplate = null;
            if (vertices.IsCreated) vertices.Dispose();
            vertices = default;
            localTemplate?.Dispose();
            localTemplate = null;
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseResources;
#endif
            ReleaseResources();
        }
    }
}
