using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace MarchingCube
{
    public class ModelPool : MonoBehaviour, IObjectPool<Model>
    {
        [Header("Optional model prefab")]
        public GameObject root;
        [SerializeField, Min(1)] private int maxCount = 100;
        [SerializeField, Min(0)] private int preWarmCount = 10;
        [SerializeField] private bool autoExpand;
        private readonly Queue<Model> inactiveModels = new Queue<Model>();
        private readonly HashSet<Model> activeModels = new HashSet<Model>();
        private readonly List<Model> allModels = new List<Model>();
        public int CountInactive => inactiveModels.Count;
        public int CountActive => activeModels.Count;
        public int MaxCount => maxCount;
        public int PreWarmCount => preWarmCount;

        public void ConfigureCapacity(int maximum)
        {
            maxCount = Mathf.Max(1, maximum);
            preWarmCount = Mathf.Min(preWarmCount, maxCount);
        }

        private void Awake() => PreWarm();

        public void PreWarm()
        {
            int target = Mathf.Min(preWarmCount, maxCount);
            while (allModels.Count < target) inactiveModels.Enqueue(CreateNewModel());
        }

        public Model Get()
        {
            Model model;
            if (inactiveModels.Count > 0) model = inactiveModels.Dequeue();
            else
            {
                if (allModels.Count >= maxCount)
                {
                    if (!autoExpand) { Debug.LogWarning("Pool limit reached"); return null; }
                    maxCount = Mathf.Max(maxCount + 1, maxCount * 2);
                }
                model = CreateNewModel();
            }
            activeModels.Add(model);
            model.gameObject.SetActive(true);
            return model;
        }

        public PooledObject<Model> Get(out Model value)
        {
            value = Get();
            return new PooledObject<Model>(value, this);
        }

        public void Release(Model model)
        {
            if (model == null) return;
            if (!activeModels.Remove(model)) { Debug.LogWarning("Releasing item not active in this pool"); return; }
            model.Deformation.Hide();
            model.Cell = null;
            model.Library = null;
            model.gameObject.SetActive(false);
            inactiveModels.Enqueue(model);
        }

        public void Clear()
        {
            foreach (var model in allModels)
            {
                if (model.gameObject == null) continue;
                model.Deformation.ReleaseResources();
                if (Application.isPlaying) Destroy(model.gameObject);
                else DestroyImmediate(model.gameObject);
            }
            activeModels.Clear();
            inactiveModels.Clear();
            allModels.Clear();
        }

        private void OnDestroy() => Clear();

        private Model CreateNewModel()
        {
            GameObject obj;
            if (root == null || root == gameObject)
                obj = new GameObject("Procedural Model");
            else
            {
                if (root.GetComponentInChildren<ModelPool>(true) != null)
                    throw new InvalidOperationException("The model prefab must not contain a ModelPool.");
                obj = Instantiate(root);
            }
            obj.SetActive(false);
            obj.transform.SetParent(transform, false);
            var filter = obj.GetComponent<MeshFilter>();
            if (filter == null) filter = obj.AddComponent<MeshFilter>();
            var renderer = obj.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = obj.AddComponent<MeshRenderer>();
            // Install the resource owner before activation, including for prewarmed models.
            if (obj.GetComponent<MeshDeformation>() == null) obj.AddComponent<MeshDeformation>();
            var model = new Model { MeshFilter = filter, MeshRenderer = renderer };
            allModels.Add(model);
            return model;
        }
    }
}
