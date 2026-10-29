# 原始四边形松弛算法归档

2026-10-29，正式松弛统一采用 `QuadMeshOptimizer`（Balanced）。

`LegacyQuadRelaxation.cs` 保留原逐面、固定目标半径、四元数旋转的更新算法。此文件受 `UNITY_EDITOR` 保护，只用于历史结果复现和性能对比，不进入 Player 构建，也不允许成为正常生成路径的备用分支。

归档入口是 `Editor.Archive.LegacyQuadRelaxation.Apply(...)`。原始数学运算、遍历顺序和步长保持不变；仅将所属位置从 `HexGrid` 移出，并显式传入网格参数。一个回归用例验证 12×8、种子 42、自由边界 30 次的历史面积膨胀和面积变异系数。

历史对照工具已集中到 Unity 的 `Tools > Marching Cube > Archive > Relaxation` 菜单。普通生成器、建筑演示、地形演示及 `HexGrid.RelaxGrid(...)` 均使用 Balanced。不要把运行时算法枚举或自动兜底重新引入生产入口。
