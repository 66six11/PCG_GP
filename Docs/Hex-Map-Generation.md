# 战棋式六边形地图生成

地图范围由六边形坐标集合决定。每个坐标表示一个实际六边形的中心，先共享相邻六边形的边角点并生成六个三角形，再沿用 `RandomMergeTriangles → SubdivideGrid → RelaxGrid → BuildLayers → BuildCells`，生成不规则四边形和八角点 Cell。

## 配置入口

`UnifiedGridBuildingDemo` 默认启用 `Use Coordinate Layout`，在 Inspector 的 `Map Layout` 中设置：

- `Mode = Rectangle`：`Width` 和 `Height` 是六边形列数、行数，采用奇数列错位排列。
- `Excluded Tiles`：从矩形中移除的 `(列, 行)`，从 `(0, 0)` 开始，可形成缺角、通道、内部空洞及多个分离区域。超出矩形的掩码项不影响生成。
- `Mode = Coordinates`：`Coordinates` 直接列出保留六边形的轴向 `(q, r)`，支持负坐标。
- `Center On Origin`：把现有六边形中心的包围盒中心对齐传入的世界原点。关闭后，轴向 `(0, 0)` 对应原点。

`GirdGenerator` 为兼容 SampleScene 默认仍使用原半径模式，勾选 `Use Coordinate Layout` 后使用相同的 `Map Layout` 配置。取消勾选也可以让统一建筑 Demo 使用旧 `Grid Radius`。

修改配置后重新生成或重新进入 Play Mode。本阶段只改变水平范围和 Cell 生成，不增加高度编辑功能。

控制器的 `Relax Iterations` 可调整松弛次数，默认 30。正式松弛已统一为 Balanced；历史算法的对比数据见 [面积与松弛对比](Hex-Map-Relaxation-Results.md)。

`Preserve Boundary` 默认关闭，外边界和孔洞边界一起参与 Balanced 优化；开启后固定这些边界。两个演示场景均明确配置为 30 次、自由边界。

不再提供算法选择项。面积、边长与角度共同优化的实现与数据见 [尺寸均衡算法](Balanced-Quad-Relaxation.md)。原算法仅保留在 Editor 归档工具中。

`UnifiedGridBuildingDemo.unity` 和 `BuildingModuleAuthoringDemo.unity` 已配置为 12×8、移除 4 格的示例，包含内部空洞及外边界缺口，共 92 个源六边形。

## 代码入口

```csharp
var layout = new HexGridLayout {
    width = 12,
    height = 8,
    excludedTiles = new List<Vector2Int> {
        new Vector2Int(5, 3), // 空洞
        new Vector2Int(11, 0) // 缺角
    }
};
var grid = layout.CreateGrid(2, Vector3.zero, 1.35f, 14);
grid.RandomMergeTriangles();
grid.SubdivideGrid();
grid.RelaxGrid(5);
grid.BuildLayers();
grid.BuildCells();
```

底层也可直接构建：`new HexGrid(IEnumerable<Coord>, cellSize, origin, cellHeight, layerCount)`。重复坐标去重，输入按 q/r 排序，因此相同坐标集合与随机种子不会因为输入顺序不同而改变生成结果。输入集合会复制，不保留可变调用方列表。

## 拓扑约定

- 新生成路径不枚举半径环，不创建掩码外的地图，也不在缺失的六边形之间自动补面。
- 六边形角点使用整数格点键共享身份，避免浮点距离焊接和全图搜索；正常情况下六个源三角形对应一个六边形的覆盖范围。
- `RelaxGrid` 和 `RelaxBalanced` 都执行 Balanced。直接调用 `RelaxGrid(times)` 时，边界缺省策略仍按地图类型选择：坐标地图固定、半径地图自由；统一建筑控制器则显式传入 Inspector 的边界设置，默认自由。可通过 `RelaxGrid(times, preserveBoundary)` 明确设置。
- 同一规范顶点仍向上复制，生成水平层和建筑 Cell；分块、WFC 与撤销继续使用现有数据接口。
- `HexCoordinates` 是保留的源六边形坐标；仅新路径有效。新路径的 `radius = -1`，不得用其推导地图范围；调用 `RingVertices` 会明确报错。
- 空坐标集合可在底层生成空拓扑，建筑 Demo 则要求至少一个六边形。

旧半径构造函数的坐标表示三角格点，保留其环状拼接、排序及结果以兼容旧调用。新构造函数的坐标表示完整六边形瓦片，两种入口的相同数值坐标不代表相同覆盖范围。

## 验证与保存

运行 `Tools > Marching Cube > Unified Grid Demo > Run Regression Tests`。测试包含单瓦片、狭长矩形、缺口、孔洞、分离区域、负坐标、去重与顺序确定性、边界保持、面积覆盖、Cell 邻接，以及掩码地图的碰撞拾取、WFC 和撤销/重做。

2026-10-28：83 项回归测试通过，0 失败。结果保存在 `Logs/UnifiedGridDemo/tests.xml`。

地图配置或松弛算法版本改变会改变拓扑指纹。现有 v3 保存格式保持不变，不同拓扑的存档会被拒绝。原算法生成的存档不能仅靠当前生成参数恢复旧指纹，需要迁移或重新生成；正式入口不会自动回退到归档算法。
