# 规格增量 — 战斗 HUD 的局内蓝量 v0.0.1

## MODIFIED R10 — Ricca 生命与蓝量

### 场景: 进入或重新载入 Demo
- 当 Demo 载入 Ricca 和战斗 HUD
- 则 Application 提供满额的 HP 与 MP，HP 和 MP 的默认初始值均为 100，HUD 左上显示两者的当前量与上限

### 场景: Ricca 实际受伤
- 当 已初始化的 Ricca 收到正伤害且 HP 大于 0
- 则 Application 扣除相应 HP，最低为 0，在同一结算中清空连击和续连窗口。HUD 同帧更新血条并隐藏连击，蓝条继续显示当前 MP

### 场景: 没有实际损失生命
- 当 Ricca 收到零或负伤害，或当前 HP 已为 0
- 则 Application 维持当前生命、蓝量和连击

### 场景: 施放接受与缺蓝
- 当 技能施放被接受或因当前 MP 不足被拒绝
- 则 Application 提供对应的当前 MP，HUD 使用快照更新蓝条。缺蓝时选择界面显示禁用的技能与 10 MP 成本提示
