namespace JustEnoughItems.UI
{
    public static class HoverContext
    {
        private static string _hoveredTechType;
        public static void SetHoveredTechType(string techType)
        {
            if (string.IsNullOrEmpty(techType)) { _hoveredTechType = null; return; }
            var t = techType.Trim();
            if (t.StartsWith("TechType.", System.StringComparison.OrdinalIgnoreCase))
                t = t.Substring("TechType.".Length);
            _hoveredTechType = t.Trim();
        }
        public static void ClearHoveredTechType()
        {
            _hoveredTechType = null;
        }
        public static string GetHoveredTechType()
        {
            return _hoveredTechType;
        }
    }
}
