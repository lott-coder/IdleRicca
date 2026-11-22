# Git 代码库配置

将 IdleRicca 接入 GitHub 私有仓库 `lott-coder/_ricca`，在 `main` 上跟踪源代码、测试、程序集定义、源码元数据、Unity 工程配置和项目文档。

`.gitignore` 将 `Assets/` 下的纳入范围限定为 `Editor`、`Scripts` 和 `Tests` 中的源码、程序集定义及元数据。场景、预制体、美术资源、游戏配置资产、Unity 生成目录和归档备份由忽略规则覆盖。`.gitattributes` 为 C#、程序集定义、JSON 和元数据指定 LF。

## 验证

- `git ls-files --others --exclude-standard` 显示 138 项 Unity 源码相关文件。核对了源码与 `.meta` 的配对，以及项目版本、依赖声明和依赖锁的纳入状态。
- `git check-ignore -v` 命中城市地形、Demo 场景、游戏配置、预制体、TextMesh Pro、生成目录和模拟新增贴图的忽略规则。
- `git ls-remote origin` 确认首次提交前远端为空。
- 需要场景或预制体的 Unity 测试须在带有本地资产的工程中运行。
