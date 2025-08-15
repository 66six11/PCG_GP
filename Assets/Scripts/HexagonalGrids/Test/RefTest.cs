using System;
using UnityEngine;

namespace HexagonalGrids.Test
{
    public class RefTest : MonoBehaviour
    {
        public A a;

        private void Awake()
        {
            a = new A();
        }

        private void Start()
        {
            Debug.Log("RefTest is being started");
        }

        private void OnEnable()
        {
            if (a != null)
            {
                Debug.Log("a is not null");
                if (a.B1 != null)
                {
                    Debug.Log("a.B1 is not null");
                }
                else
                {
                    Debug.Log("a.B1 is null");
                }
            }
            else
            {
                Debug.Log("a is null");
            }
        }

        private void OnDisable()
        {
        }
    }

    public class A
    {
        private B b;
        public B B1 => b;

        public A()
        {
            b = new B();
            Debug.Log("A is being created");
        }

        ~A()
        {
            Debug.Log("A is being destroyed");
        }
    }

    public class B
    {
        public B()
        {
            Debug.Log("B is being created");
        }

        ~B()
        {
            Debug.Log("B is being destroyed");
        }
    }
}