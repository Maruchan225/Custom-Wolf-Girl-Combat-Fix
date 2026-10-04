# GirlPool Combat Fix

Small fix for fuwuvi's Uma Musume mod for Family Time.

## What it fixes

There is a bug that prevents custom Wolf Girls from attacking.

This plugin fixes that issue so custom Wolf Girls can be sent to attack normally.

## Requirements

- BepInEx
- CustomModelsLoader by fuwuvi

## Installation

Put:

`GirlPoolCombatFix.dll`

inside:

`BepInEx/plugins/`

## Building

Requires the .NET SDK, .NET Framework 4.8 targeting pack, and the game assemblies.

Set `GamePath` to your Family Time installation directory:

```powershell
dotnet build GirlPoolCombatFix.csproj -c Release -p:GamePath="PATH_TO_GAME"
```

Alternatively, create `GirlPoolCombatFix.local.props` next to the project:

```xml
<Project>
  <PropertyGroup>
    <GamePath>PATH_TO_GAME</GamePath>
  </PropertyGroup>
</Project>
```

The local properties file is excluded from Git to keep machine-specific paths private.

Algorithm: SHA256

Hash: 8617C5E6427259ADFA149CC7544225D5CB71B21508B3313CFCECB82A2752A538

https://www.virustotal.com/gui/file/8617c5e6427259adfa149cc7544225d5cb71b21508b3313cfcecb82a2752a538

## Credits

Fix made with help from Codex.

Original CustomModelsLoader by fuwuvi.
