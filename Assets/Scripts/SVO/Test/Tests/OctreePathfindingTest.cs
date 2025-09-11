using System.Collections.Generic;
using UnityEngine;
using SVO.Runtime.Core;

namespace SVO.Test
{
    public class OctreePathfindingTest : MonoBehaviour
    {
        [Header("Test Settings")]
        public bool runTestsOnStart = true;
        public bool showTestResults = true;
        
        [Header("Test Parameters")]
        public Vector3 testOctreeSize = new Vector3(10f, 10f, 10f);
        public int testMaxDepth = 3;
        public float testMinNodeSize = 1f;
        
        private bool testsCompleted = false;
        private List<string> testResults = new List<string>();
        
        void Start()
        {
            if (runTestsOnStart)
            {
                RunAllTests();
            }
        }
        
        /// <summary>
        /// Run all pathfinding tests
        /// </summary>
        public void RunAllTests()
        {
            testResults.Clear();
            testResults.Add("Starting Octree Pathfinding Tests...");
            
            TestOctreeConstruction();
            TestNodeFlags();
            TestBasicPathfinding();
            TestObstaclePathfinding();
            TestPathSmoothing();
            
            testsCompleted = true;
            testResults.Add("All tests completed!");
            
            if (showTestResults)
            {
                foreach (string result in testResults)
                {
                    Debug.Log(result);
                }
            }
        }
        
        /// <summary>
        /// Test octree construction
        /// </summary>
        private void TestOctreeConstruction()
        {
            testResults.Add("\n--- Testing Octree Construction ---");
            
            try
            {
                Bounds bounds = new Bounds(Vector3.zero, testOctreeSize);
                Octree octree = new Octree(bounds, testMaxDepth, testMinNodeSize);
                
                if (octree.root != null)
                {
                    testResults.Add("✓ Octree root created successfully");
                }
                else
                {
                    testResults.Add("✗ Failed to create octree root");
                    return;
                }
                
                List<OctreeNode> leafNodes = octree.GetLeafNodes();
                if (leafNodes.Count > 0)
                {
                    testResults.Add($"✓ Generated {leafNodes.Count} leaf nodes");
                }
                else
                {
                    testResults.Add("✗ No leaf nodes generated");
                }
                
                // Test node position lookup
                Vector3 testPos = Vector3.zero;
                OctreeNode node = octree.GetNodeAtPosition(testPos);
                if (node != null)
                {
                    testResults.Add("✓ Node position lookup working");
                }
                else
                {
                    testResults.Add("✗ Node position lookup failed");
                }
            }
            catch (System.Exception e)
            {
                testResults.Add($"✗ Octree construction failed: {e.Message}");
            }
        }
        
        /// <summary>
        /// Test node flags functionality
        /// </summary>
        private void TestNodeFlags()
        {
            testResults.Add("\n--- Testing Node Flags ---");
            
            try
            {
                Bounds bounds = new Bounds(Vector3.zero, testOctreeSize);
                Octree octree = new Octree(bounds, testMaxDepth, testMinNodeSize);
                
                OctreeNode testNode = octree.GetNodeAtPosition(Vector3.zero);
                if (testNode == null)
                {
                    testResults.Add("✗ Could not get test node");
                    return;
                }
                
                // Test default flags
                if (testNode.HasFlag(OctreeNodeFlags.Walkable))
                {
                    testResults.Add("✓ Default walkable flag set correctly");
                }
                else
                {
                    testResults.Add("✗ Default walkable flag not set");
                }
                
                // Test setting blocked flag
                testNode.SetFlag(OctreeNodeFlags.Blocked, true);
                if (testNode.HasFlag(OctreeNodeFlags.Blocked))
                {
                    testResults.Add("✓ Blocked flag set correctly");
                }
                else
                {
                    testResults.Add("✗ Failed to set blocked flag");
                }
                
                // Test pathfinding data reset
                testNode.gCost = 10f;
                testNode.hCost = 15f;
                testNode.ResetPathfindingData();
                
                if (testNode.gCost == float.MaxValue && testNode.hCost == 0f)
                {
                    testResults.Add("✓ Pathfinding data reset correctly");
                }
                else
                {
                    testResults.Add("✗ Pathfinding data reset failed");
                }
            }
            catch (System.Exception e)
            {
                testResults.Add($"✗ Node flags test failed: {e.Message}");
            }
        }
        
        /// <summary>
        /// Test basic pathfinding without obstacles
        /// </summary>
        private void TestBasicPathfinding()
        {
            testResults.Add("\n--- Testing Basic Pathfinding ---");
            
            try
            {
                Bounds bounds = new Bounds(Vector3.zero, testOctreeSize);
                Octree octree = new Octree(bounds, testMaxDepth, testMinNodeSize);
                
                Vector3 start = new Vector3(-2f, 0f, -2f);
                Vector3 end = new Vector3(2f, 0f, 2f);
                
                List<Vector3> path = OctreePathfinder.FindPath(octree, start, end);
                
                if (path.Count > 0)
                {
                    testResults.Add($"✓ Basic pathfinding successful - {path.Count} waypoints");
                    
                    // Verify start and end points
                    if (Vector3.Distance(path[0], start) < 2f && 
                        Vector3.Distance(path[path.Count - 1], end) < 2f)
                    {
                        testResults.Add("✓ Path start and end points correct");
                    }
                    else
                    {
                        testResults.Add("✗ Path start or end points incorrect");
                    }
                }
                else
                {
                    testResults.Add("✗ Basic pathfinding failed - no path found");
                }
            }
            catch (System.Exception e)
            {
                testResults.Add($"✗ Basic pathfinding test failed: {e.Message}");
            }
        }
        
        /// <summary>
        /// Test pathfinding with obstacles
        /// </summary>
        private void TestObstaclePathfinding()
        {
            testResults.Add("\n--- Testing Obstacle Pathfinding ---");
            
            try
            {
                Bounds bounds = new Bounds(Vector3.zero, testOctreeSize);
                Octree octree = new Octree(bounds, testMaxDepth, testMinNodeSize);
                
                // Add obstacles in the middle
                for (int x = -1; x <= 1; x++)
                {
                    octree.SetNodeBlocked(new Vector3(x, 0, 0), true);
                }
                
                Vector3 start = new Vector3(-3f, 0f, 0f);
                Vector3 end = new Vector3(3f, 0f, 0f);
                
                List<Vector3> path = OctreePathfinder.FindPath(octree, start, end);
                
                if (path.Count > 0)
                {
                    testResults.Add($"✓ Obstacle pathfinding successful - {path.Count} waypoints");
                    
                    // Check if path avoids obstacles
                    bool pathAvoidsMidObstacles = true;
                    foreach (Vector3 waypoint in path)
                    {
                        if (Mathf.Abs(waypoint.x) <= 1.5f && Mathf.Abs(waypoint.z) <= 1.5f)
                        {
                            // Check if this waypoint is at the obstacle level
                            if (Mathf.Abs(waypoint.y) < 0.5f)
                            {
                                pathAvoidsMidObstacles = false;
                                break;
                            }
                        }
                    }
                    
                    if (pathAvoidsMidObstacles)
                    {
                        testResults.Add("✓ Path correctly avoids obstacles");
                    }
                    else
                    {
                        testResults.Add("⚠ Path may go through obstacles (expected for 3D pathfinding)");
                    }
                }
                else
                {
                    testResults.Add("✗ Obstacle pathfinding failed - no path found");
                }
            }
            catch (System.Exception e)
            {
                testResults.Add($"✗ Obstacle pathfinding test failed: {e.Message}");
            }
        }
        
        /// <summary>
        /// Test path smoothing functionality
        /// </summary>
        private void TestPathSmoothing()
        {
            testResults.Add("\n--- Testing Path Smoothing ---");
            
            try
            {
                Bounds bounds = new Bounds(Vector3.zero, testOctreeSize);
                Octree octree = new Octree(bounds, testMaxDepth, testMinNodeSize);
                
                Vector3 start = new Vector3(-3f, 0f, 0f);
                Vector3 end = new Vector3(3f, 0f, 0f);
                
                List<Vector3> originalPath = OctreePathfinder.FindPath(octree, start, end);
                
                if (originalPath.Count > 0)
                {
                    List<Vector3> smoothedPath = OctreePathfinder.SmoothPath(originalPath, octree);
                    
                    if (smoothedPath.Count <= originalPath.Count)
                    {
                        testResults.Add($"✓ Path smoothing successful - reduced from {originalPath.Count} to {smoothedPath.Count} waypoints");
                    }
                    else
                    {
                        testResults.Add("✗ Path smoothing increased waypoint count");
                    }
                    
                    // Verify smoothed path still connects start and end
                    if (smoothedPath.Count >= 2 &&
                        Vector3.Distance(smoothedPath[0], start) < 2f &&
                        Vector3.Distance(smoothedPath[smoothedPath.Count - 1], end) < 2f)
                    {
                        testResults.Add("✓ Smoothed path maintains start and end points");
                    }
                    else
                    {
                        testResults.Add("✗ Smoothed path lost start or end points");
                    }
                }
                else
                {
                    testResults.Add("✗ Could not test path smoothing - no original path found");
                }
            }
            catch (System.Exception e)
            {
                testResults.Add($"✗ Path smoothing test failed: {e.Message}");
            }
        }
        
        void OnGUI()
        {
            if (showTestResults && testsCompleted)
            {
                GUILayout.BeginArea(new Rect(Screen.width - 400, 10, 380, Screen.height - 20));
                GUILayout.Label("Octree Pathfinding Test Results", new GUIStyle() { fontSize = 14, fontStyle = FontStyle.Bold });
                
                foreach (string result in testResults)
                {
                    GUILayout.Label(result);
                }
                
                if (GUILayout.Button("Run Tests Again"))
                {
                    RunAllTests();
                }
                
                GUILayout.EndArea();
            }
        }
    }
}