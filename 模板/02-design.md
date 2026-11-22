# 技术设计文档: <主题>

> 模块: {module}
> 日期: YYYY-MM-DD
> 状态: 草稿 / 已确认设计
> 权威模式: 本地 / 专用服务器
> 在途契约终检: <待执行 / 0 冲突（比对 N 个在途模块） YYYY-MM-DD>

## ★ 核心重点（一目了然区）

> 看这部分就知道怎么干。

### ★1. 架构总览
<新增/修改什么，落在哪一层，一句话概括>

### ★2. 关键类型清单
| 类型 | 层 | 职责 | 依赖 |
|------|----|------|------|
| XxxService | Application | 唯一写入口 | ISaveStore |
| XxxConfig | Config (SO) | 数值配置 | — |

### ★3. 接入点
- 程序集：`Game.Application.asmdef`（是否需要新增）
- 注册：组合根 / DI 容器注册位置
- 配置：`Assets/Config/Xxx.asset`（新增或改字段）
- 存档：是否新增分区 `SaveData.xxx`
- 入口：哪个 Presentation 层组件调用

### ★4. 核心数据流
<配置 → Application → Domain → 持久化 → Presentation 的完整路径>

### ★5. 实现步骤概要
| 步骤 | 内容 | 依赖 |
|------|------|------|

---

## 详细设计（补齐区）

### 1. 架构设计
#### 1.1 模块定位
<在分层中的位置，与哪些现有类型交互，不交互谁>

#### 1.2 类型结构
```csharp
// 关键类型签名（不必完整实现）
public sealed class XxxService
{
    public XxxService(ISaveStore saves, XxxConfig config) { }
    public Result Buy(string itemId, int count) { }
}
```

#### 1.3 依赖方向
<谁依赖谁；是否引入新的 asmdef 引用>

### 2. 数据结构
#### 2.1 核心类型
```csharp
public readonly record struct XxxState(int Level, long Exp);
```

#### 2.2 接口 / 事件契约（C1）
| 名称 | 类型 | 方向 | 参数 | 说明 |
|------|------|------|------|------|
| Buy | 方法 | Presentation→Application | (string itemId, int count) → Result | 购买请求 |
| Purchased | event | Application→Presentation | (string itemId, long total) | 购买完成 |

#### 2.3 存档影响（C2）
- 分区：`SaveData.economy`（新增 / 修改）
- 字段变更：
- 迁移方案：<无变更 / 版本 N→N+1 的迁移规则>
- 旧存档兼容：<可读 / 需迁移 / 不兼容（需说明理由）>

### 3. 交互流程
#### 3.1 时序
```
Presentation 点击 → Application.Buy()
  → 校验（类型/范围/余额）
  → Domain 计算（价格、折扣）
  → 写入存档（整分区）
  → 触发 Purchased 事件
  → Presentation 刷新
```

#### 3.2 生命周期
<初始化序、Ready 关卡、订阅与退订点>

#### 3.3 错误处理与兜底
| 失败点 | 表现 | 处理 |
|---|---|---|
| 余额不足 | Fail("INSUFFICIENT") | 不做任何变更 |
| 存档写失败 | Fail("IO") | 回滚内存状态 + 提示 |

### 4. 性能与防御性设计
#### 4.1 分配预算
<热路径是否分配；对象池；缓存的引用>
#### 4.2 性能红线
- `Update` 内禁止：`new`、LINQ、字符串拼接、`GetComponent`、`Find`
- 大列表遍历禁止 O(N×M)
#### 4.3 防御性编码
- 幂等：<哪些操作需要标记>
- 终态守卫：<哪些方法需要状态判断>
- 清理顺序：停回调 → 退订 → 释放
#### 4.4 内存/实例预估
<新增常驻对象、配置资产体积>

### 5. 架构约束遵守声明
- [ ] 类型放在正确层，未引入反向依赖
- [ ] `Application` 是唯一写入口，`Presentation` 不直接改状态
- [ ] 权威模式与 `架构.md` 一致
- [ ] 外部输入已净化
- [ ] 事件订阅/退订成对
- [ ] 存档变更有迁移或兼容说明
- [ ] 等待都有超时
- [ ] 每帧路径无分配
- [ ] 验证策略可执行（用例 + 级别）

### 6. 验证策略
| 用例 | 级别 | 覆盖的 AC | 构造方式 |
|------|------|----------|---------|
| Buy_Succeeds | L1 | AC-1 | 内存 store 替身 |
| Buy_Insufficient_NoChange | L1 | AC-2 | 内存 store 替身 |

### 7. 风险评估
| 风险 | 影响 | 应对 |
|---|---|---|
