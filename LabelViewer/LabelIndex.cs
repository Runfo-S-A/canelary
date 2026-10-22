using Core.Services.BackendModel;

namespace LabelViewer;

public record ProductEntry(short Id, string Code, string Name, string Senasa, List<string> Labels);

public static class LabelIndex
{
    /// <summary>
    /// Invierte etiqueta → productos en producto → etiquetas.
    /// </summary>
    public static List<ProductEntry> Build(IEnumerable<(string Label, List<Product> Products)> labels)
    {
        return [.. labels
            .SelectMany(l => l.Products.Select(p => (l.Label, Product: p)))
            .GroupBy(x => x.Product.Id)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var p = g.First().Product;
                return new ProductEntry(p.Id, p.Code, p.Name, p.Senasa, [.. g.Select(x => x.Label).Distinct()]);
            })];
    }
}
