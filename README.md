# 极速双遗物 · Blitz Double Relics

《杀戮尖塔 2》极速联机的宝箱提供两轮选遗物。每轮每位玩家可选一件，也可以跳过。两人选中同一件时，继续用游戏原有的猜拳和落选补偿规则。第一轮用过的候选会从第二轮移除。

适用于 Steam 测试版 `v0.113.1` 的 2–4 人极速联机。双人、三人、四人局分别在开箱时显示 4、6、8 件候选。普通模式和单人极速保持游戏原有规则。所有玩家都需要安装同一版本。本 Mod 可以单独使用。

## 安装

1. 下载 Releases 中的 `BlitzDoubleRelics-0.1.2.zip`。
2. 退出游戏。在 Steam 库中右键游戏，选择「管理」→「浏览本地文件」。
3. 将压缩包内的 `BlitzDoubleRelics` 文件夹解压到游戏目录下的 `mods`，保留同目录内的 DLL 和 JSON。
4. 所有玩家启动游戏。`godot.log` 中的 `[BlitzDoubleRelics] 0.1.2 active` 表示当前游戏已经启用规则。

仅在已核对的 `v0.113.1` 修订 `d09d48e5` 上启用。更新游戏后，其他版本会使用官方规则。如果官方已安排两轮，Mod 会跳过该宝箱。切换到其他版本后可取消订阅，让所有玩家使用相同的 Mod 列表。

双人联机已验证两轮选择和争抢猜拳。`0.1.2` 的手势回归和四人界面仍待实机确认。

## English

Blitz treasure rooms offer two rounds of shared relic picks in 2–4 player co-op. You may pick one relic or skip in each round. The game still resolves contested picks with its usual rock-paper-scissors and consolation award. Relics awarded in round one are removed from the second round.

Every player needs the same mod version. Requires the Slay the Spire 2 Steam beta `v0.113.1`. Standard and solo Blitz treasure rooms retain their existing behavior. No mod dependencies.

Extract the `BlitzDoubleRelics` folder from the release zip into the game's `mods` folder, then launch the game. The `[BlitzDoubleRelics] 0.1.2 active` line in `godot.log` confirms the rules are active. Only the reviewed game revision `d09d48e5` is enabled. Other game versions use their native rules, and a chest already configured for two rounds is left to the game. When moving to a newer game version, unsubscribe on all players to keep their mod lists in sync. Two-player selection and contested picks were verified live on the first release. The updated hand animation and four-player layout still await live verification.

## Build from source

Use the .NET 9 SDK or newer and a local game installation. Set `STS2_MANAGED_DIR` to the folder containing `sts2.dll`, `0Harmony.dll`, and `GodotSharp.dll`.

```powershell
$env:STS2_MANAGED_DIR = 'C:\path\to\Slay the Spire 2\data_sts2_windows_x86_64'
dotnet build src/BlitzDoubleRelics.csproj -c Release
dotnet run --project tests/BlitzDoubleRelics.Checks.csproj -c Release -- $env:STS2_MANAGED_DIR
```

The mod uses the game's built-in synchronized pick actions, award resolution and relic UI. The supplied checks exercise patch binding, vote reset, and holder and candidate order after simultaneous awards. To package a build, put `BlitzDoubleRelics.dll` and `BlitzDoubleRelics.json` from `src/bin/Release/net9.0/` in a `BlitzDoubleRelics` folder.

Original mod source is available under the [MIT license](LICENSE). Game libraries and assets are obtained from your own installation.
