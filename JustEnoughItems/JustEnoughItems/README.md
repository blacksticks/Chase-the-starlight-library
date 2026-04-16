# JustEnoughItems (Subnautica)

- BepInEx 5.4.23.3
- Nautilus 1.0.0-pre47
- VS2022, 纯引用，无 NuGet

## 构建

1. 编辑根目录 `Directory.Build.props`，设置：
   - `GameDir` 指向 Subnautica 安装目录，例如：
     - `D:\Steam\steamapps\common\Subnautica`
2. 打开 `JustEnoughItems.sln`，以 Release 构建。
3. 若设置了 `GameDir`，会自动复制 DLL 至 `BepInEx\plugins\JustEnoughItems`。

## 配置文件

- 运行一次游戏或手动在 `BepInEx/config/JustEnoughItems/jei.json` 放置配置。
- 模板位于项目 `config-template/jei.json`。

### 配置结构

```
{
  "Items": [
    {
      "ItemId": "TechType.Titanium",
      "Sources": [ { "Station": "Fabricator", "Instrument": "", "Description": "...", "RelatedItemIds": [] } ],
      "Uses":    [ { "Station": "HabitatBuilder", "Instrument": "", "Description": "...", "RelatedItemIds": ["TechType.Builder"] } ]
    }
  ]
}
```

- ItemId：任意字符串（推荐使用 TechType 名称）。
- Sources/Uses：
  - Station：工作台或来源标签名。
  - Instrument：特殊获得仪器名，可留空。
  - Description：描述文本。
  - RelatedItemIds：与该条目关联的其他物品 ID。

## 功能进度

- 热键 G：打开/关闭 JEI 窗口。
- 后续将：
  - 替换 PDA 蓝图页为 JEI 页面。
  - 背包悬停提示“按 G 查看合成配方”，并跳转对应物品配置页。
