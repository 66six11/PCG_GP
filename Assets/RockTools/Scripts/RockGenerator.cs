using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace RockTools
{
    public class RockGenerator : MonoBehaviour
    {
        // ----------------------------------------------------------------------------------------

        [SerializeField] public int rndSeed;
        [SerializeField] public ERockType type;
        [SerializeField] private Material material;
        [SerializeField] public LogicBase[] logics = new LogicBase[RockTypeExtensions.RockTypesLenght];

        public int pRndSeed
        {
            get => rndSeed;
            set
            {
                if (value != rndSeed)
                {
                    rndSeed = value;
                    UpdateRock();
                }
            }
        }

        public ERockType pType
        {
            get => type;
            set
            {
                if (value != type)
                {
                    type = value;
                    UpdateRock();
                }
            }
        }

        public Material pMaterial
        {
            get => material;
            set
            {
                if (value != material)
                {
                    material = value;
                    UpdateMaterials();
                }
            }
        }

        public MeshFilter pMeshFilter
        {
            get
            {
                if (meshFilter == null)
                {
                    meshFilter = GetComponent<MeshFilter>();
                }

                return meshFilter;
            }
        }

        public MeshRenderer pMeshRenderer
        {
            get
            {
                if (meshRenderer == null)
                {
                    meshRenderer = GetComponent<MeshRenderer>();
                }

                return meshRenderer;
            }
        }

        // ----------------------------------------------------------------------------------------

        private MeshBuffer meshBuffer = new MeshBuffer(200000);
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Random random;

        // ----------------------------------------------------------------------------------------

        public static RockGenerator GetInstance()
        {
            return new GameObject("Rock Generator").AddComponent<RockGenerator>();
        }

        // ----------------------------------------------------------------------------------------

        private void Reset()
        {
            material = null;
            if (meshRenderer)
            {
                meshRenderer.sharedMaterial = material;
            }

            UpdateRock();
        }

        // ----------------------------------------------------------------------------------------

        public void UpdateRock()
        {
            PrepareRockGeneratorLogic();
            PrepareDefaultMaterial();
            PrepareComponents();
            RefreshRandomSeeds();

            meshBuffer.Clear();

            logics[type.GetTypeIndex()]?.UpdateRock(ref meshBuffer, random);

            BuildMesh();
        }

        // ----------------------------------------------------------------------------------------

        private void PrepareRockGeneratorLogic()
        {
            var rockTypes = EnumUtil.GetValues<ERockType>().ToArray();
            for (var i = 0; i < logics.Length; i++)
            {
                if (ReferenceEquals(logics[i], null))
                {
                    logics[i] = rockTypes[i].GetLogicInstance();
                }
            }
        }

        // ----------------------------------------------------------------------------------------

        public void Randomize(int increment)
        {
            rndSeed += increment;
            UpdateRock();
        }

        // ----------------------------------------------------------------------------------------

        private void PrepareDefaultMaterial()
        {
            if (material == null)
            {
                var defaultCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var diffuse = defaultCube.GetComponent<MeshRenderer>().sharedMaterial;
                material = diffuse;
                if (Application.isPlaying)
                    Destroy(defaultCube);
                else
                    DestroyImmediate(defaultCube);
            }
        }

        // ----------------------------------------------------------------------------------------

        private void PrepareComponents()
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();

                if (meshFilter == null)
                {
                    meshFilter = gameObject.AddComponent<MeshFilter>();
                }
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();

                if (meshRenderer == null)
                {
                    meshRenderer = gameObject.AddComponent<MeshRenderer>();
                }
            }

            if (meshRenderer)
            {
                if (meshRenderer.sharedMaterial == null)
                {
                    meshRenderer.sharedMaterial = material;
                }
                else
                {
                    material = meshRenderer.sharedMaterial;
                }
            }
        }

        // ----------------------------------------------------------------------------------------

        private void RefreshRandomSeeds()
        {
            random = new Random(rndSeed);
        }

        // ----------------------------------------------------------------------------------------

        private void BuildMesh()
        {
            var mesh = meshBuffer.GetMesh();
            mesh.name = gameObject.name;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            meshFilter.sharedMesh = mesh;
        }

        // ----------------------------------------------------------------------------------------

        public void UpdateMaterials()
        {
            meshRenderer.sharedMaterial = material;
        }

        // ----------------------------------------------------------------------------------------

        public int GetVertexCount()
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return -1;
            }

            return meshFilter.sharedMesh.vertexCount;
        }

        // ----------------------------------------------------------------------------------------
    }
}