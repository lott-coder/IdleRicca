# Ricca 普攻卡肉验证

## 配置

`Assets/Config/Gameplay/RiccaNormalAttack.asset`。Inspector 中的 `Hit Stop Seconds` 默认 0.10，`Hit Stop Multiplier` 默认 0.05。

剑身结算确认有效扣血后，Ricca 和被命中敌人的 Animator 分别以共用倍率播放。计时使用真实秒数。受击与死亡动作的超时按局部动画速率累计，新命中刷新计时，同帧多目标共用本帧的截止点。

## 验证结果

| 项目 | 结果 | 证据 |
|---|---|---|
| Unity 2022.3 编译 | 退出码 0，C# 错误与警告 0 | `Logs/HitStopVerification/HitStop-Final-Compile.log` |
| EditMode | 35/35，零跳过 | `Logs/HitStopVerification/HitStop-Final-EditMode.xml` |
| PlayMode | 11/11，零跳过 | `Logs/HitStopVerification/HitStop-Final-PlayMode.xml` |
| 配置与实际命中 | 验证默认值、空挥、未命中角色、局部减速、原速恢复、真实秒数、后续命中刷新、不累乘及致死动作 | `Assets/Tests/PlayMode/RiccaGameplayTests.cs` |
| 画面 | Unity PlayMode 取景 | `Logs/HitStopVerification/Ricca-After-Hit.png` |
| 安装 | 主工程 10 个文件与已验证副本的 SHA-256 一致 | `Logs/HitStopVerification/installed-sha256.json` |

Animator 时序与局部计时的研究参考和实现记录见 `knowledge/gameplay.md` 的「卡肉」。手感由后续真人试玩判断。
