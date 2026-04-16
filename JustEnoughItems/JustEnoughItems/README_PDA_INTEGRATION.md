# JustEnoughItems PDA集成说明

## 概述
本实现为JustEnoughItems模组添加了PDA页签功能，允许玩家在游戏PDA中直接查看JEI的物品配方信息。

## 功能特性
- **PDA页签集成**：在PDA界面中添加专用的JEI页签
- **统一配方显示**：完全按照JSON配置文件显示配方，不读取游戏内置配方
- **图标和本地化**：包含页签图标和多语言支持
- **无缝交互**：与PDA的原生导航和控制完全兼容

## 实现架构

### 核心组件

#### 1. uGUI_JEITab.cs
- 继承自`uGUI_PDATab`的自定义页签类
- 负责管理JEI UI在PDA中的显示和隐藏
- 处理页签生命周期事件

#### 2. PDATabExtension.cs
- 使用Nautilus的EnumHandler扩展PDATab枚举
- 包含Harmony补丁将JEI页签注册到PDA系统
- 管理页签在PDA tabs字典中的注册

#### 3. JEITabSpriteHandler.cs
- 处理JEI页签的图标资源
- 提供本地化文本支持
- 创建默认图标作为后备方案

#### 4. JeiUI.cs (修改)
- 添加`ShowForPDATab()`方法用于PDA页签显示
- 保持原有的独立窗口功能

### 集成流程

1. **初始化阶段**
   - Plugin.cs中注册JEI PDA页签
   - PDATabExtension扩展枚举并注册页签实例

2. **PDA集成**
   - Harmony补丁修改uGUI_PDA.Initialize()添加页签
   - 修改SetTabs()方法将JEI页签加入页签列表

3. **UI显示**
   - uGUI_JEITab管理JEI UI的显示状态
   - JeiUI提供PDA专用的显示方法

## 使用方法

### 配置文件
JEI的配方配置文件位于：
```
JustEnoughItems/config.json
```

配置文件格式示例：
```json
{
  "items": [
    {
      "itemId": "scanner",
      "displayName": "扫描仪",
      "techType": "Electronics",
      "icon": "scanner_icon",
      "sources": [
        {
          "type": "craft",
          "station": "fabricator",
          "ingredients": [
            {"itemId": "battery", "amount": 1},
            {"itemId": "copper", "amount": 2}
          ]
        }
      ]
    }
  ],
  "categories": [
    {
      "id": "Electronics",
      "name": "电子产品",
      "order": 1
    }
  ]
}
```

### 访问JEI页签
1. 在游戏中打开PDA
2. 点击新增的JEI页签（显示为"JEI"或"物品配方"）
3. 浏览配置的物品和配方信息

## 技术细节

### Harmony补丁
- `uGUI_PDA.Initialize` - 注册JEI页签实例
- `uGUI_PDA.SetTabs` - 添加页签到页签列表
- `SpriteManager.Get` - 提供页签图标
- `Language.Get` - 提供本地化文本

### 兼容性
- 基于Nautilus 1.0.0-pre47
- 兼容BepInEx 5.4.23.3
- 支持Subnautica 83031

### 扩展性
- 支持自定义页签图标（通过AssetBundle）
- 支持多语言本地化
- 可与其他PDA扩展模组共存

## 故障排除

### 常见问题

1. **JEI页签未显示**
   - 检查日志中的注册错误
   - 确认Nautilus正确加载
   - 验证Harmony补丁是否成功应用

2. **配置文件未加载**
   - 检查config.json文件路径
   - 验证JSON格式是否正确
   - 查看控制台的配置加载日志

3. **UI显示异常**
   - 检查AssetBundle是否正确加载
   - 验证UI预制体路径
   - 确认Canvas和RectTransform设置

### 调试信息
启用调试模式可在Plugin.cs中设置：
```csharp
DebugAutoOpen.Value = true; // 自动打开JEI进行测试
```

## 开发说明

### 添加新功能
1. 扩展JeiItem类添加新属性
2. 更新JeiUI的显示逻辑
3. 修改配置文件格式
4. 更新本地化文本

### 自定义图标
1. 准备64x64像素的PNG图标
2. 添加到AssetBundle中
3. 在JEITabSpriteHandler中加载自定义图标

## 许可证
本项目遵循原JustEnoughItems模组的许可证条款。
