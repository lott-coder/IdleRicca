# City 迁移问题与验证

源场景为 `D:/Learning/Unity/Mster/project_ntr/Assets/Scenes/City.unity`，源 GUID 为 `bfb455dcabaad7c47870537bf1d6f6c8`。交付仍由原 `Assets/Scenes/Demo.unity` 和原 GUID 承接。原 Room 场景及素材已移到 `Logs/Room-before-city`。

| 现象 | 处理 |
|---|---|
| 地形的纹理层在 YAML 依赖扫描中缺失 | 从二进制 TerrainData 的外部引用恢复七个地形层和各自纹理 |
| 花坛为白模 | 将源花材的 `_Base_Color` 和 `_Normal` 接入 Built-in Standard，按源阈值用 Alpha Cutout |
| 源透明玻璃与不透明窗帘名称相近 | 按 `_Surface` 与 `_AlphaClip` 配置队列、混合和深度写入 |
| 初始机位车辆遮挡及角色与车厢重叠 | 保留车辆场景道具，角色和主机位移至开阔街段，确认四名角色脚下均有 City 地形碰撞 |
| 隔离副本首次解析 UPM 遇到网络重置 | 复用当前工程相同版本的软件包内容，仅在隔离副本使用。正式测试显式选择项目的两个测试程序集并使用 D3D11 |

## 验收

| 用例 | 判据 | 结果及证据 |
|---|---|---|
| L0 编译 | Unity 2022.3.52f1c1 无 C# 编译错误，编译退出码 0 | 通过，`Logs/CityVerification/city-compile.log` |
| L1 结构、贴图、碰撞 | 构建序号 0 使用 City 内容，3814 个 MeshRenderer 与 2543 个 LODGroup，地形层、道路和建筑贴图、花坛及玻璃均通过校验，四角色脚底射线落在 City 环境上 | 5/5，`Logs/CityVerification/city-final-editmode.xml` |
| L2 动画 | 四个现有 Prefab 的 Idle、Move、Attack 实际驱动骨骼，攻击结束返回待机 | 通过，`Logs/CityVerification/city-final-playmode.xml` |
| L4 台前与街景 | 实际运行主镜头和角色近景，以及整区斜俯视截图 | 路面、街区墙面、招牌、植被与四角色均可辨，`Logs/CityDemo/Main.png`、`Overview.png` 和角色近景 |
| L4 光影 | 固定机位间隔 30 帧对比 120 帧内的静态街景，比较变化像素与幅度 | 518,400 像素中各次变化 120、24、115、23，最大差值 1/255，`Logs/CityVerification/city-final-playmode.log` |

PlayMode 共 2/2。主工程安装后核验 977 个文件与已验证副本的 SHA-256 完全一致。场景引用闭包含 481 个资源，未发现缺失或重复 GUID。Room 已移出 Assets。
