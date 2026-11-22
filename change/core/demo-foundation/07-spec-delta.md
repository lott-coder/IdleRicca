# core 规格增量 v0.0.1

## ADDED R1 — Demo 启动场景

### 场景: 启动 Demo
- 当 启动构建或打开 Demo 主场景进入 Play
- 则 Unity 载入源自 Mster 首关 Room 的房间，开启主相机和固定光照，实例化 Ricca 以及哥布林、骷髅和地狱犬

## ADDED R2 — 可复用的 Demo 角色资产

### 场景: 角色动画运行
- 当 在 Demo 中实例化任一角色预制体，并播放 Idle、Move 或 Attack
- 则 Unity Animator 将对应动作应用到角色骨骼，房间和角色使用可解析的 Built-in 材质，攻击完成后返回待机
