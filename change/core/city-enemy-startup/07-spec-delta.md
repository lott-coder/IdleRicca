# 规格增量 - core v0.0.1

> gameplay/enemy-spawn-ai v0.0.1 的配套规格变更。需求、设计与审核见该迭代文档。

## MODIFIED R1 — Demo 启动场景

### 场景: 启动 Demo
- 当 启动构建或打开 Demo 主场景进入 Play
- 则 Unity 载入源自 Mster City 的市中心，开启主相机和固定光照，实例化 Ricca，Application 根据 gameplay R7 初始化敌人生成，默认优先各生成一只哥布林、骷髅和地狱犬，并遵守场景上限和安全位置条件
