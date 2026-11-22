# Core 模块知识

> 模块：core
> 关联层：Unity 场景与表现资源
> 最后更新：2026-11-22

## 跨 Unity 工程迁移场景

- 用源场景的 GUID 对照导出版本与整理版本，然后以场景所需网格、材质、贴图为起点递归检查引用。资源分别成组迁入并带上 `.meta`。导入后用 Unity 保存最终场景与预制体，并复核构建入口和缺失引用。
- AssetRipper 导出的合批场景有一组 Renderer 指向同一个 combined mesh。同一网格、变换及材质可能负责不同墙面或地板。若检查两者重叠，还需要比较 `m_StaticBatchInfo.firstSubMesh`、`subMeshCount` 和材质槽顺序。保留子网格范围可以继续使用原来的合批网格。
- 转换材质前读取实际保存的纹理及 UV 参数。源 URP/Toon 的 `_BaseMap` 接入 Standard 的 `_MainTex`，法线与发光贴图分别检查。源材质可能有保存的发光颜色，但关键字关闭。直接用颜色是否非零决定发光会让墙面过曝。透明物体按 Cutout 或 Fade 配置混合、队列和深度写入。
- 室内光用室内点光源校验，避免用室外方向光穿过天花板照亮角色。拍摄首个 Play 画面前要等 Animator 求值。比较静态阴影的采样帧时，先让测试里隐藏角色的变更完成。D3D11 下实测存在零散像素的 8 位末位差异，记录幅度和数量便于区分读回舍入与肉眼可见的明暗波动。

## 提取可独立播放的角色

- 先在源工程序列化文件里定位 Animator 与模型，保留相对动画路径、Avatar 与蒙皮骨骼引用。删除某个可选网格时，先核对它的 Transform 是否也被其他蒙皮引用。
- Ricca 源预制体的装备 Renderer 有占位网格和临时材质。其 Runtime 装配配方在 `rck_k_clothes_armor`、`rck_k_arm_armor`、`rck_k_foot` 等装备配置的 `meshSetList`。按配置绑定网格、完整材质槽和身体裁片可以减少衣甲穿插。
- 精选基础动作时移除依赖原脚本的 AnimationEvent 和对象引用曲线，保留骨骼曲线。用两时点骨骼姿态差异验证驱动链。并列 Animator 过渡会按登记顺序匹配，需要让攻击过渡先于移动状态切换。

## 资料
- [AssetRipper Shader 导出选项说明](https://github.com/AssetRipper/AssetRipper/blob/master/Localizations/en_US.json)
- [Unity Built-in Standard 混合模式实现](https://github.com/TwoTailsGames/Unity-Built-in-Shaders/blob/master/Editor/StandardShaderGUI.cs)

## City 材质及地形迁移

- TerrainData 可能是二进制 Unity SerializedFile。仅扫描 YAML 会漏掉 TerrainLayer 引用，地形形状仍能显示，地表却失去颜色与法线。先读取外部引用表，再按源资产 GUID 复制各层及贴图。之后在 Unity 中验证 Terrain Collider 共用数据，以及每层的主贴图和法线。 [Unity SerializedFile 外部引用](https://github.com/Unity-Technologies/UnityDataTools/blob/main/Documentation/command-serialized-file.md)
- 转换 City 玻璃及贴花时，优先查源 `_Surface` 和 `_AlphaClip`。名字相近的 `Glass_House` 与 `Glass_House_Fade` 使用不同渲染队列和深度写入。花坛使用 `_Base_Color` 和 `_Normal`，两项需要显式映射。该纹理的 Alpha 通道用于 Cutout。
- 原编辑器打开时，可在隔离副本使用同版本包完成导入与测试，安装后对相同资产做 SHA-256 比对。隔离副本使用嵌入包时，测试命令要筛选项目自己的程序集，避免将包内测试误计入门禁。
