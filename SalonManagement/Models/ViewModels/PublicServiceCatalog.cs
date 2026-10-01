namespace SalonManagement.Models.ViewModels;

public sealed class PublicServiceCatalog
{
    public List<PublicServiceGroup> Groups { get; init; } = [];
    public bool HasServices => Groups.Any(g => g.Services.Count > 0);
}

public sealed class PublicServiceGroup
{
    public int? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public List<Service> Services { get; set; } = [];
}
