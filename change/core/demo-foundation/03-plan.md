# 执行计划 v0.0.1

| 状态 | 内容 | 输出 |
|---|---|---|
| 完成 | 识别 Room 和四种角色所需子树及资源 | 依赖清单 |
| 完成 | 导入最小资源闭包，处理旧材质与灯光 | Assets/Art/Demo |
| 完成 | 创建预制体、控制器和主场景构建入口 | Assets/Prefabs/Demo、Assets/Scenes/Demo.unity |
| 完成 | 执行 Unity 检查、动画与图像验证 | Logs、回归测试 |
| 完成 | 处理审核反馈，归纳经验并校验归档 | change/core/demo-foundation、knowledge/core.md |

## 验证与回滚
先验证资源闭包和 Unity 编译，再核对真实 Play 与图像。迁移目录独立。回滚可恢复原 Build Settings，移除本次独立资源和测试目录。

### 回归用例
- L1 核验构建入口、场景实例来源、有效网格、骨骼、贴图及缺失脚本。
- L2 逐角色验证三种状态确实改变骨骼姿势，使用 Moving 和 Attack 参数并检查攻击结束返回待机。
- L4 保留 Game 视角和角色近景，连续采样静态区域检测光影变动。
- 疑难问题优先查 GitHub 原始实现，查询与实际采用的结论留在问题记录。
