# PCG 建造原型

新增统一网格地形与多处自由建造版本：打开 `Assets/Scenes/UnifiedGridBuildingDemo.unity` 后按 Play。
操作和实现边界见 [UnifiedGridBuildingDemo.md](UnifiedGridBuildingDemo.md)。下文保留第一个平地建造 demo 的说明。

打开 `Assets/Scenes/SampleScene.unity`，点击 Unity 的 Play 即可运行。
原型复用场景中 HexGrid 的生成器和 PCG 模型库，启动时显示一个带入口的基础建筑。

- 左键拖动：按当前模式添加或移除网格点。
- 右键拖动：临时擦除。
- Q / E 或高度按钮：切换编辑平面。
- 中键拖动：旋转；Shift + 中键拖动：平移；滚轮：缩放。
- Ctrl+Z / Ctrl+Y：撤销 / 重做。一次连续拖动是一个操作。
- 载入示例、清空：均可撤销。
- 黄色十字表示当前高度已启用的点，球形光标表示将要编辑的点。

编辑以顶点启用状态为准：单个顶点变化只标记与其关联的 Cell，随后刷新这些
Cell 的模型。完全空或完全实心的 Cell 不需要表面模型。渲染实例由对象池复用。
网格线和启用点分别使用一个网格显示，避免为每条边创建独立对象。

编辑状态只保留在当前运行中。再次进入 Play 会重新生成网格和示例；本原型
尚不保存建筑，也不更新建筑碰撞或寻路数据。

需要把原型接到另一个已有网格的场景时，选择
`Tools > Marching Cube > Prototype > Setup Current Scene`。它会绑定当前生成器、
模型库、材质和相机，将高度扩展到至少四层，并停用旧的网格/模型预览脚本。
场景配置支持 Unity 撤销；已有源模型和网格半径不变。

`GridEditSession` 独立管理事务、撤销栈和脏单元；`GridPrototype` 负责输入、
平面选点、视角、显示和模型池生命周期。无需物理碰撞体即可编辑。
