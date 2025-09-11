using System.Collections.Generic;

namespace Utility.RefCopy
{
    public static class RefIDGenerator
    {
        private static int _id = 0;
        private static readonly object _lock = new object();

        public static int ID
        {
            get
            {
                lock (_lock)
                {
                    return _id++;
                }
            }
        }

        public static void ResetID()
        {
            lock (_lock)
            {
                _id = 0;
            }
        }
    }

    public interface ICopyable<out T>
    {
        public int id { get; set; }
        T Copy();
    }

    public interface IRefCopy<T1, out T2> where T1 : ICopyable<T1>
    {
        public T2 Copy(Dictionary<int, T1> refMap);
    }

    public class RefCopyHelper<T> where T : ICopyable<T>
    {
        private Dictionary<int, T> _refMap = new Dictionary<int, T>();

        public RefCopyHelper(List<T> list, out List<T> copyList)
        {
            foreach (T item in list)
            {
                _refMap.Add(item.id, item.Copy());
            }

            copyList = new List<T>(_refMap.Values);
        }


        public List<T1> ListCopy<T1>(List<T1> obj) where T1 : IRefCopy<T, T1>
        {
            List<T1> list = new List<T1>();
            foreach (T1 item in obj)
            {
                list.Add(item.Copy(_refMap));
            }

            return list;
        }

        public List<T> ListCopy(List<T> obj)
        {
            List<T> list = new List<T>();
            foreach (T item in obj)
            {
                list.Add(_refMap[item.id]);
            }

            return list;
        }
    }
}