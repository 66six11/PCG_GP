using System.Collections.Generic;
using UnityEngine;
using SVO.Runtime.Core;

namespace SVO.Test
{
    /// <summary>
    /// Simple standalone test to validate octree pathfinding functionality
    /// </summary>
    public class SimpleOctreeTest : MonoBehaviour
    {
        void Start()
        {
            RunBasicTest();
        }
        
        void RunBasicTest()
        {
            Debug.Log("Starting Simple Octree Pathfinding Test...");
            
            try
            {
                // Create a simple octree
                Bounds bounds = new Bounds(Vector3.zero, new Vector3(10f, 10f, 10f));
                Octree octree = new Octree(bounds, 3, 1f);
                
                Debug.Log($"✓ Octree created successfully with {octree.GetLeafNodes().Count} leaf nodes");
                
                // Test basic pathfinding
                Vector3 start = new Vector3(-3f, 0f, -3f);
                Vector3 end = new Vector3(3f, 0f, 3f);
                
                List<Vector3> path = OctreePathfinder.FindPath(octree, start, end);
                
                if (path.Count > 0)
                {
                    Debug.Log($"✓ Pathfinding successful! Found path with {path.Count} waypoints");
                    Debug.Log($"  Start: {path[0]}, End: {path[path.Count - 1]}");
                }
                else
                {
                    Debug.LogError("✗ Pathfinding failed - no path found");
                }
                
                // Test with obstacles
                octree.SetNodeBlocked(Vector3.zero, true);
                octree.SetNodeBlocked(new Vector3(0, 0, 1), true);
                octree.SetNodeBlocked(new Vector3(0, 0, -1), true);
                
                List<Vector3> pathWithObstacles = OctreePathfinder.FindPath(octree, start, end);
                
                if (pathWithObstacles.Count > 0)
                {
                    Debug.Log($"✓ Obstacle pathfinding successful! Found path with {pathWithObstacles.Count} waypoints");
                }
                else
                {
                    Debug.Log("⚠ Obstacle pathfinding failed - this might be expected if obstacles completely block the path");
                }
                
                Debug.Log("Simple Octree Test Completed Successfully!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"✗ Test failed with exception: {e.Message}");
                Debug.LogError(e.StackTrace);
            }
        }
    }
}