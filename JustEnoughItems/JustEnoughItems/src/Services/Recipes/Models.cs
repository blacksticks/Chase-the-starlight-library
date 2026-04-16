using System.Collections.Generic;

namespace JustEnoughItems
{
    public class RecipeIngredient
    {
        public string TechId { get; set; } = string.Empty;
        public int Amount { get; set; } = 1;
    }

    public class RecipeEntry
    {
        public string FabricatorId { get; set; } = string.Empty;
        public string FabricatorDisplayName { get; set; } = string.Empty;
        public List<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
        public List<string> Products { get; set; } = new List<string>();
    }

    public class ScanResult
    {
        public List<RecipeEntry> Recipes { get; set; } = new List<RecipeEntry>();
    }
}
