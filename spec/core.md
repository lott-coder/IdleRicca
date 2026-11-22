# core 模块规格（活规格基线）

> 由 change/core/city-enemy-startup v0.0.1 的 07-spec-delta.md 归档合并生成（2026-11-22）。
> 最后归档 v0.0.1 @ 2026-11-22
<!-- spec_tool delta sha256: fdb31132c4d9a14cc1dbf00d7a997497c12c8c5b0486cbaaadb48f0cc424aece -->
> 本文件是当前真相源（what）；只读——更新唯一入口是 /workflow archive 的 delta 合并。

---

## R1 — Demo 启动场景（MODIFIED v0.0.1）

### 场景: 启动 Demo
- 当 启动构建或打开 Demo 主场景进入 Play
- 则 Unity 载入源自 Mster City 的市中心，开启主相机和固定光照，实例化 Ricca，Application 根据 gameplay R7 初始化敌人生成，默认优先各生成一只哥布林、骷髅和地狱犬，并遵守场景上限和安全位置条件

## R2 — 可复用的 Demo 角色资产（MODIFIED v0.0.1）

### 场景: 角色动画运行
- 当 在 Demo 中实例化任一角色预制体，并播放 Idle、Move 或 Attack
- 则 Unity Animator 将对应动作应用到角色骨骼，街道和角色使用可解析的 Built-in 材质，攻击完成后返回待机

