# Demo 基础设施技术设计

> 在途契约终检：全局索引仅 core 在途，无模块冲突。

## 核心重点
用 Mster Room 的独立场景副本保留房间结构、道具与碰撞。使用单相机和固定 Built-in 灯光配置。保留必要的材质、贴图与网格引用，重新映射 URP 贴图属性和透明模式。
在 _ricca 的场景内按 Animator 查到完整骨架及蒙皮，提取到四个 Prefab。骨骼相对路径原样保留，精选源动画并构建独立的小型 Animator Controller。剥离源游戏事件和脚本依赖。各保留 Idle、Move、Attack 状态，Attack 播完回到 Idle。

## 详细设计
- 在主场景以 Player 标签标记 Ricca，敌人各自保留独立 Prefab 引用。
- 资源闭包按项目和 GUID 处理，保留必要的 .meta 并通过 Unity 导入和重新序列化。对使用旧管线或占位 shader 的材质做兼容处理。
- 验证入口检查 build index、Prefab 与 Animator 状态、实际骨骼运动、网格、贴图、组件与影像。原始数据只读。
- 本轮交付序列化资产与测试。玩法接口及存档格式无变更。

## 源资产落位
- Room 正式来源为 Mster 的整理工程 `project_ntr/Assets/Scenes/Room.unity`。它与初次导出的 `level1.unity` 共用 GUID `075fe44d7dfc1094788d1d52ffd719d7`。
- 四个角色以 _ricca 整理工程 `work/project/Assets/Resources/prefab/character/` 的 `RicassoKnightActor`、`GoblinActor`、`SkeletonActor`、`HellhoundActor` 为源。提取 Animator 下的骨骼和渲染层级，按原装备配置装配 Ricca 的铠甲、手甲和鞋，应用铠甲配置指定的身体网格。
- 室内机位照向客厅。两盏固定的 Built-in 点光源提供主光和补光，主光投射软阴影。
- 进入 Play 后各角色默认待机。在 Animator 参数面板勾选 `Moving` 播放原地移动，取消勾选回待机。触发 `Attack` 优先于移动转换，攻击播完先回待机，随后按当前 `Moving` 取值继续。
