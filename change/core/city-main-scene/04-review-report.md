# City 迁移审核

用户已确认换成 City 并授权无人值守，保留逐项验证与独立审核。

## P1–P3 审核
独立审核要求给材质、道路碰撞、LOD、Terrain 与发布顺序明确判据。已准备按 Build index、City 根节点、道路和建筑贴图槽、LOD 有效引用、地形数据、四角色脚底碰撞、原动作和室外固定机位采样复验。隔离副本排除 Room，主工程先移出 Room 再装回已验证资产，以免两个来源重复使用同一 GUID。

## P4 审核
独立代码审查检查了 City 的 MeshFilter、LODGroup、Terrain、构建 GUID 和材质。按反馈保留源 `_Surface` 指定的不透明/透明类型，覆盖 `Glass_House` 与 `Glass_House_Fade` 的深度写入及渲染队列。逐个检验七个地形层的主贴图与法线。另通过预览发现并恢复花材独有的主贴图、法线和 Alpha Cutout。重新执行 Unity 门禁并检查最终主工程的资源哈希后放行。
