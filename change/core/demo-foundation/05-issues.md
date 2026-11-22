# 问题与验证

## 已处理的问题

| 现象 | 定位 | 处理 |
|---|---|---|
| 部分墙面与地板从画面消失 | 房间不同部件共享同一个合批网格，初次去重误禁用 134 个 Renderer | 按子网格起点和数量复核，保留各自绘制范围 |
| 白墙大面积过曝 | 源 White_0 材质存在非零发光颜色，同时关闭发光关键字 | 关闭 Standard 对应发光设置，使用室内主光与补光 |
| 模型显示装备占位网格 | Ricca 源工程依赖装备配置完成装配 | 绑定原装备配置的铠甲、手甲、鞋和配套身体裁片 |
| 角色从移动转攻击不稳定 | 同一帧同时取消 Moving 并触发 Attack 时，移动过渡先匹配 | 攻击优先过渡，从动作实际状态及骨骼两时点差异验证 |
| 静态截图不是逐像素完全一致 | D3D11 在 960×540 图中仅有零散像素相差一个 8 位色阶 | 固定机位复测并记录最大差值和变化面积 |

## 验证记录

Unity 2022.3.52f1c1，2026-11-22。编译退出码 0，C# 编译错误和警告均为 0。检查了四层 asmdef 白名单，未出现反向依赖。清理迁移临时场景与工具后，Demo 和四个 Prefab 的引用闭包包含 272 个资源文件及 12 个动画片段，未发现缺失或重复 GUID。

| 用例 | 操作 | 预期 | 证据 | 结果 |
|---|---|---|---|---|
| T1 主场景与房间资产 | 读取构建配置并在 Unity 中打开 Demo | 构建序号 0，实例引用正常，绘制范围完整，书籍与植物主贴图、地板与布料法线及屏幕发光贴图存在 | `Logs/demo-final-editmode.xml` | 通过 |
| T2 四种角色预制体 | 遍历 Actor Prefab 的 Animator、Avatar、骨骼、蒙皮及材质槽 | 四个 Avatar 有效，无缺失骨骼、材质或脚本，每个角色有三个有效片段 | `Logs/demo-final-editmode.xml` | 通过 |
| T3 Play 动作 | 加载 build index 0，由 Animator 参数切换待机、移动、攻击 | 每个状态实际改变蒙皮骨骼的姿势，攻击播完返回待机 | `Logs/demo-final-playmode.xml` | 通过 |
| T4 实际画面 | Animator 求值后抓取主画面、近景及相隔 0.2 秒的攻击画面 | 房间、队列与角色色彩可辨，实际运行显示不同姿势及随姿势变化的阴影 | `Logs/Demo/Main.png`、`Logs/Demo/Attack-12.png` 至 `Attack-48.png`、四张近景 | 通过 |
| T5 固定机位光影 | 场景静置，隔 30 帧采样并与首帧比较，测试跨 120 帧 | 稳定区域无可见明暗跳变 | `Logs/Demo/Room-Frame*.png`、`Logs/demo-final-playmode.log` | 通过，518,400 像素中各次变化 11、13、12、12，最大通道差 1/255 |

EditMode 5/5，PlayMode 2/2。编译证据见 `Logs/demo-compile.log`。校验范围为本机 D3D11 的 Demo 场景。

## 关联资料
- [AssetRipper Shader 导出选项](https://github.com/AssetRipper/AssetRipper/blob/master/Localizations/en_US.json)
- [Unity Standard 透明模式实现](https://github.com/TwoTailsGames/Unity-Built-in-Shaders/blob/master/Editor/StandardShaderGUI.cs)
