using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JustEnoughItems.Config
{
    // 内联：允许 JSON 属性既可为单值也可为数组
    public class InlineSingleOrArrayConverter<T> : JsonConverter
    {
        public override bool CanConvert(System.Type objectType)
        {
            return objectType == typeof(List<T>);
        }

        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            var list = new List<T>();
            if (token.Type == JTokenType.Array)
            {
                foreach (var el in token)
                {
                    using (var r = el.CreateReader())
                    {
                        list.Add(serializer.Deserialize<T>(r));
                    }
                }
            }
            else if (token.Type != JTokenType.Null && token.Type != JTokenType.Undefined)
            {
                using (var r = token.CreateReader())
                {
                    list.Add(serializer.Deserialize<T>(r));
                }
            }
            return list;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var list = value as List<T>;
            if (list == null || list.Count == 0)
            {
                writer.WriteStartArray();
                writer.WriteEndArray();
                return;
            }
            if (list.Count == 1)
                serializer.Serialize(writer, list[0]);
            else
                serializer.Serialize(writer, list);
        }
    }
    // 顶层 items.json：{ "Items": [ { ... } ] }
    public class JeiConfig
    {
        public List<JeiItem> Items { get; set; } = new List<JeiItem>();
    }

    // 单个物品的 JEI 数据（图标默认按 ItemId 从 icon 目录读取，同名覆盖；Icon 字段可选覆盖路径或名称）
    public class JeiItem
    {
        public string ItemId { get; set; } = string.Empty;           // 物品ID（与游戏内 TechType 名称或其他ID对应）
        public string DisplayName { get; set; } = string.Empty;       // 可选：显示名称（默认回退 ItemId）
        [JsonProperty("ChineseName")]
        public string ChineseName { get; set; } = string.Empty;       // 新增：物品中文名称，仅通过 JSON 定义
        public string Icon { get; set; } = string.Empty;              // 兼容旧字段：不再推荐使用
        public string Patch { get; set; } = string.Empty;             // 新字段：相对 icons 目录的路径，例如 "Item/TiIngot.png"
        public string Description { get; set; } = string.Empty;       // 可选：物品简介

        // 物品来源（可配置多个页签）
        public List<JeiSourceTab> Source { get; set; } = new List<JeiSourceTab>();

        // 物品用途（可配置多个页签）
        public List<JeiUsageTab> Usage { get; set; } = new List<JeiUsageTab>();
    }

    // 页签基类：统一可选文本与图片
    public abstract class JeiTabBase
    {
        public bool IfFabricator { get; set; } = false;     // 是否工作台模板
        public string Text { get; set; } = string.Empty;     // 说明文本
        [JsonProperty("Patch")]
        public string Patch { get; set; } = string.Empty;    // 图标或展示图片的相对路径（位于 icons 目录）。替代旧字段 Image。
        [JsonProperty("Image")]
        public string Image { get; set; } = string.Empty;    // 兼容旧字段：将被代码回退读取
        [JsonProperty("TabIcon")]
        public string TabIcon { get; set; } = string.Empty;  // 新增：仅用于顶部 MethodTab 的图标；IfFabricator=false 时优先生效
    }

    // 来源页签：IfFabricator=true 时，展示“在哪个工作台 + 配方 -> 本物品”；否则展示图片与文本
    public class JeiSourceTab : JeiTabBase
    {
        public string Fabricator { get; set; } = string.Empty;        // 工作台ID
        [JsonProperty("FabricatorDisplayName")]
        public string FabricatorDisplayName { get; set; } = string.Empty; // 新增：工作台显示名称
        public List<string> Ingredient { get; set; } = new List<string>(); // 合成原料（按输入数量列出）
    }

    // 用途页签：IfFabricator=true 时，展示“在哪个工作台 + 配方(含当前物品) -> 目标物品”；否则展示图片/文本/目标
    public class JeiUsageTab : JeiTabBase
    {
        public string Fabricator { get; set; } = string.Empty;        // 工作台ID
        [JsonProperty("FabricatorDisplayName")]
        public string FabricatorDisplayName { get; set; } = string.Empty; // 新增：工作台显示名称
        public List<string> Ingredient { get; set; } = new List<string>(); // 合成原料（按输入数量列出）
        [JsonProperty("Target")]
        [JsonConverter(typeof(InlineSingleOrArrayConverter<string>))]
        public List<string> Target { get; set; } = new List<string>(); // 用途侧右侧网格目标，支持单个或数组
    }
}
