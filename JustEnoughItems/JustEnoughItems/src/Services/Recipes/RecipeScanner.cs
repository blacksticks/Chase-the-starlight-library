using System.Collections.Generic;

namespace JustEnoughItems
{
    public class RecipeScanner
    {
        private readonly List<IRecipeProvider> _providers = new List<IRecipeProvider>();

        public RecipeScanner()
        {
            // 优先：Nautilus 提供者（适配 BepInEx+Nautilus 新环境）
            try { _providers.Add(new NautilusRecipeProvider()); } catch { }
            // 回退：通用反射扫描（CraftData/TechData + 兼容旧版 SMLHelper 通道）
            try { _providers.Add(new SmlHelperRecipeProvider()); } catch { }
        }

        public ScanResult ScanAll()
        {
            var result = new ScanResult();
            foreach (var p in _providers)
            {
                var r = p.Scan();
                if (r?.Recipes != null && r.Recipes.Count > 0)
                {
                    result.Recipes.AddRange(r.Recipes);
                }
            }
            return result;
        }
    }
}
