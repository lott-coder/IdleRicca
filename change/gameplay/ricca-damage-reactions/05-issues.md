# Ricca 受击与死亡动作验证

- 原工程控制器 ricasso_knight_animationController 将普通受伤与普通死亡分别绑定到 rc_stand_damage 和 rc_death。IdleRicca 工程原先缺少两段片段。
- 通过 Unity Editor 导入两段片段，保留骨骼曲线，清除源工程专用事件和对象引用，Hit 与 Death 均为非循环。
- 普通受击切入 Hit，随后回到既有待机和移动流程。有效伤害取消已开始的挥击，归零切入 Death 并保留末帧。重新激活同一个归零对象时恢复末帧，重载场景从初始 HP 与 Idle 开始。
- 独立 Unity 副本编译无 C# 错误或警告。EditMode 35 通过，PlayMode 10 通过，无失败或跳过。
- 实际敌人命中和角色骨骼取样已验证。截图位于 Logs/RiccaReaction/Ricca-Hit.png 和 Logs/RiccaReaction/Ricca-Death.png。
- 安装后的 11 个文件 SHA-256 均与独立验证版本一致。源工程文件保持原样。
