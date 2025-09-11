# Octree Pathfinding System

This implementation provides a complete 3D octree-based pathfinding system with visualization for Unity.

## Features

### Core Pathfinding
- **3D Octree Structure**: Hierarchical space partitioning for efficient 3D navigation
- **A* Pathfinding Algorithm**: Optimal pathfinding with configurable heuristics
- **Obstacle Support**: Dynamic obstacle placement and removal
- **Path Smoothing**: Reduces unnecessary waypoints for cleaner paths
- **26-Directional Movement**: Full 3D neighbor detection for smooth movement

### Visualization
- **Real-time Octree Visualization**: Color-coded wireframe visualization of octree nodes
- **Path Visualization**: Line renderer with waypoint markers
- **Interactive Demo**: Mouse and keyboard controls for testing
- **Debug Information**: Comprehensive logging and test results

## Components

### Core Classes

#### `OctreeNode`
- Enhanced with pathfinding data (g-cost, h-cost, path parent)
- Flag system for different node states (Walkable, Blocked, Visited, etc.)
- Automatic pathfinding data reset functionality

#### `Octree`
- Manages the complete octree structure
- Configurable depth and minimum node size
- Node lookup and neighbor detection
- Dynamic obstacle management

#### `OctreePathfinder`
- Static class implementing A* pathfinding algorithm
- Manhattan distance heuristic optimized for 3D
- Path smoothing with line-of-sight optimization
- Comprehensive path validation

### Visualization Components

#### `OctreeVisualizer`
- Real-time octree structure visualization
- Color-coded node states:
  - White: Normal/Walkable nodes
  - Red: Blocked/Obstacle nodes
  - Green: Path nodes
  - Yellow: Open set (being evaluated)
  - Blue: Closed set (already evaluated)
- Configurable display options (leaf nodes only, show obstacles, etc.)

#### `PathVisualizer`
- Line renderer for path display
- Waypoint markers (spheres or custom prefabs)
- Color coding for start (green) and end (red) points
- Path clearing and updating functionality

### Demo and Testing

#### `OctreePathfindingDemo`
- Complete interactive demo system
- Mouse controls for point selection and obstacle placement
- Keyboard shortcuts for mode switching
- Real-time pathfinding and visualization
- GUI display with control instructions

#### `OctreePathfindingTest`
- Comprehensive test suite
- Tests octree construction, pathfinding, obstacles, and smoothing
- Performance validation
- Automated result reporting

## Usage

### Basic Setup

1. **Create an Octree Visualizer**:
   ```csharp
   // Add OctreeVisualizer component to a GameObject
   OctreeVisualizer visualizer = gameObject.AddComponent<OctreeVisualizer>();
   visualizer.octreeSize = new Vector3(20f, 20f, 20f);
   visualizer.maxDepth = 4;
   visualizer.minNodeSize = 1f;
   ```

2. **Add Path Visualization**:
   ```csharp
   // Add PathVisualizer component (requires LineRenderer)
   PathVisualizer pathViz = gameObject.AddComponent<PathVisualizer>();
   pathViz.pathColor = Color.green;
   pathViz.showWaypoints = true;
   ```

3. **Setup Interactive Demo**:
   ```csharp
   // Add the demo component for full interactivity
   OctreePathfindingDemo demo = gameObject.AddComponent<OctreePathfindingDemo>();
   demo.octreeVisualizer = visualizer;
   demo.pathVisualizer = pathViz;
   ```

### Programmatic Usage

```csharp
// Create octree
Bounds bounds = new Bounds(Vector3.zero, new Vector3(20f, 20f, 20f));
Octree octree = new Octree(bounds, maxDepth: 4, minNodeSize: 1f);

// Add obstacles
octree.SetNodeBlocked(new Vector3(0, 0, 0), true);
octree.SetNodeBlocked(new Vector3(1, 0, 0), true);

// Find path
Vector3 start = new Vector3(-5f, 0f, -5f);
Vector3 end = new Vector3(5f, 0f, 5f);
List<Vector3> path = OctreePathfinder.FindPath(octree, start, end);

// Smooth path (optional)
List<Vector3> smoothPath = OctreePathfinder.SmoothPath(path, octree);
```

## Controls (Interactive Demo)

### Keyboard Controls
- **1**: Set Start Position mode
- **2**: Set End Position mode  
- **3**: Add Obstacle mode
- **4**: Remove Obstacle mode
- **Space**: Find/Refresh Path
- **C**: Clear All
- **R**: Refresh Octree

### Mouse Controls
- **Left Click**: Execute current mode action (set start/end, add/remove obstacle)

## Configuration Options

### Octree Settings
- `octreeSize`: Size of the octree bounds
- `maxDepth`: Maximum subdivision depth (affects precision)
- `minNodeSize`: Minimum size of leaf nodes

### Visualization Settings
- `showOctree`: Toggle octree visualization
- `showOnlyLeafNodes`: Show only leaf nodes vs all nodes
- `showBlockedNodes`: Highlight blocked nodes
- `showPathNodes`: Highlight path nodes

### Pathfinding Settings
- `smoothPath`: Enable path smoothing
- `showPathfindingDebug`: Show detailed pathfinding information

## Performance Considerations

- **Octree Depth**: Higher depth = more precision but more memory usage
- **Node Count**: Approximately 8^depth leaf nodes for fully subdivided octree
- **Pathfinding Complexity**: O((b^d)) where b is branching factor and d is solution depth
- **Visualization**: Can be disabled in builds for better performance

## Scene Setup

The provided demo scene includes:
- Main Camera positioned for optimal viewing
- Directional Light for proper lighting
- OctreePathfindingDemo GameObject with all components configured
- Test script for automated validation

## Testing

Run the `OctreePathfindingTest` component to validate:
- Octree construction
- Node flag functionality
- Basic pathfinding
- Obstacle pathfinding
- Path smoothing

All tests include detailed logging and error reporting.

## Extension Points

The system is designed for easy extension:
- Custom heuristic functions in `OctreePathfinder`
- Additional node flags in `OctreeNodeFlags`
- Custom visualization in `OctreeVisualizer`
- Different path smoothing algorithms
- Integration with Unity NavMesh or other systems

## Dependencies

- Unity 6000.1.14f1 or later
- Unity LineRenderer component for path visualization
- No external dependencies required