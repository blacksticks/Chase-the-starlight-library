namespace JustEnoughItems
{
    public interface IRecipeProvider
    {
        string Name { get; }
        ScanResult Scan();
    }
}
