# Animal Ecosystem Usage

## 新增物种流程
1. 打开 `Tools/Animals/Setup Wizard`。
2. 指定 `Prefab Folder`，点击 `Generate Species Config Assets` 生成 `ASC_*.asset`。
3. 把对应 `AnimalSpeciesConfig` 放入场景 `AnimalSpawnZone.species` 列表。
4. 在动物 prefab 上挂载对应 Agent：
   - 熊：`BearAgent`
   - 鹿：`DeerAgent`
   - 蝴蝶：`ButterflyAgent`

## 飞行配置（isFlying）
- `isFlying=true` 后，刷新高度使用**相对地面高度**：
  - `spawnHeightMin/max`：出生时地面上方高度区间。
  - `cruiseHeightMin/max`：巡航时相对出生中心高度区间。
- 推荐起步：`spawnHeight=2~5`，`cruiseHeight=2~5`。

## 随机体型
- 开启 `enableRandomScale` 后，Spawn 时使用 `[minScale,maxScale]` 均匀随机缩放。
- 推荐起步：`minScale=0.9`，`maxScale=1.1`。

## 熊鹿捕食参数
- `bearDetectRadius`：熊发现鹿半径。
- `bearGiveUpRadius`：鹿跑太远后熊放弃。
- `bearCatchDistance`：抓到鹿判定距离。
- `bearMaxChaseTime`：追逐最长时长。
- `postCatchIdleSeconds`：抓到后原地停留时长。
- `deerFleeSpeedMultiplier`：鹿逃跑速度倍率。
- `deerFleeDurationSeconds`：鹿受惊持续时间。

## 推荐默认值
- Bear：Detect 24 / GiveUp 38 / Catch 2 / Chase 12s / PostCatch 3s
- Deer：FleeMultiplier 1.9 / FleeDuration 4s
