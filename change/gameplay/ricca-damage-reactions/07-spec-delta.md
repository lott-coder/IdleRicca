# 规格增量 - gameplay v0.0.1

## MODIFIED R5 — 剑身普攻和伤害

### 场景: 发动普通攻击
- 当 Ricca 存活、玩家按下左键且 Ricca 可以攻击
- 则 Application 接受一次普通攻击，Ricca 保持当前朝向并暂停水平移动，播放原有普通攻击动作，镜头可继续转动

### 场景: 结束普通攻击
- 当 普攻动画结束且 Ricca 存活
- 则 Application 解除移动和转身限制并继续处理当前 WASD 与 Shift

### 场景: 剑刃有效命中
- 当 有效普攻期间随剑刃运动的盒接触尚未被本次挥击命中的存活敌人
- 则 Application 对该敌人结算一次配置的普通攻击伤害，初始伤害配置为 25 HP

### 场景: 空挥、重复接触或非有效期接触
- 当 剑刃未接触敌人、已经在本次挥击命中过目标或处于非有效时间
- 则 Application 维持目标当前 HP

### 场景: Ricca 受击时已有普攻
- 当 Ricca 在普攻结束前受到有效伤害
- 则 Application 当次取消普攻，使尚未发生的命中失效

## MODIFIED R9 — 敌人的攻击盒与 Ricca 生命

### 场景: 攻击命中 Ricca
- 当 存活敌人的攻击处于有效时段，攻击盒与存活的 Ricca 受击体接触且无遮挡
- 则 Application 对 Ricca 结算一次配置伤害，Ricca 仍存活时播放普通受击动作，同一挥击后续接触不再扣血，其他角色 HP 维持原值

### 场景: 未形成有效命中
- 当 攻击被打断、Ricca 位于盒外、接触发生在窗口外或存在场景遮挡
- 则 Application 维持 Ricca 当前 HP

### 场景: Ricca 归零
- 当 有效伤害使 Ricca HP 归零
- 则 Application 停止 Ricca 的移动与攻击并使敌人停止接敌，Ricca 播放一次死亡动画并保持末帧姿势

### 场景: 重载 Demo
- 当 玩家重新加载 Demo
- 则 Application 按初始配置恢复 Ricca HP 并重建生成状态，Ricca 从待机开始
