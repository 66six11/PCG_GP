using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using System.Threading;

namespace MarchingCube
{
    //TODO: 需要重构，目前数据耦合度较高，容易出错
    public class ModelPool : MonoBehaviour, IObjectPool<Model> 
    {
        [Header("Pool Settings")]
        public GameObject root;

        [SerializeField] private int maxCount = 100;
        [SerializeField] private int preWarmCount = 10;
        [SerializeField] private bool autoExpand = false;
        // 使用更高效的数据结构
        private readonly Queue<Model> inactiveModels = new Queue<Model>();
        private readonly HashSet<Model> activeModels = new HashSet<Model>();
        private readonly List<Model> allModels = new List<Model>();

     
        // // 线程同步对象
        // private readonly object lockObj = new object();

        // 自动计算的属性（不再需要手动维护）
        public int CountInactive => inactiveModels.Count;
        public int CountActive => activeModels.Count;
        public int MaxCount => maxCount;
        public int PreWarmCount => preWarmCount;

        private void Awake()
        {
            // 预加载对象
            if (root == null)
            {
                root = gameObject;
            }

            PreWarmPool();
        }

        private void PreWarmPool()
        {
            for (int i = 0; i < Mathf.Min(preWarmCount, maxCount); i++)
            {
                Model model = CreateNewModel();
                inactiveModels.Enqueue(model);
            }
        }

        public Model Get()
        {
            // lock (lockObj)
            // {
            if (inactiveModels.Count > 0)
            {
                Model model = inactiveModels.Dequeue();
                activeModels.Add(model);
                model.gameObject.SetActive(true);
                return model;
            }

            if (allModels.Count < maxCount)
            {
                Model model = CreateNewModel();
                activeModels.Add(model);
                model.gameObject.SetActive(true);
                return model;
            }

            // 达到上限时的处理策略（可选）
            // 1. 返回null（当前行为）
            // 2. 扩展池大小（maxCount++）
            // 3. 等待并重试
            if (autoExpand)
            {
                maxCount++;
                return Get();
            }
            
            Debug.LogWarning("Pool limit reached");
            return null;
            // }
        }

        public PooledObject<Model> Get(out Model v)
        {
            v = Get();
            return new PooledObject<Model>(v, this);
        }

        public void Release(Model element)
        {
            if (element == null) return;

            // lock (lockObj)
            // {
            if (!activeModels.Contains(element))
            {
                Debug.LogWarning("Releasing item not from this pool");
                return;
            }

            activeModels.Remove(element);
            inactiveModels.Enqueue(element);
            element.gameObject.SetActive(false);
            // }
        }

        public void Clear()
        {
            // lock (lockObj)
            // {
            foreach (Model model in allModels)
            {
                if (model != null && model.gameObject != null)
                {
                    Destroy(model.gameObject);
                }
            }

            activeModels.Clear();
            inactiveModels.Clear();
            allModels.Clear();
            // }
        }

        private Model CreateNewModel()
        {
            GameObject obj = Instantiate(
                root,
                root.transform.parent
            );
            MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();
            var model = new Model()
            {
                MeshFilter = meshFilter,
                MeshRenderer = meshRenderer,
            };
            allModels.Add(model);
            return model;
        }
    }
}