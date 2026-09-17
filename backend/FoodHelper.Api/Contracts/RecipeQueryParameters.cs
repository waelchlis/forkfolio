using FoodHelper.Api.Models;

namespace FoodHelper.Api.Contracts;

public sealed class RecipeQueryParameters
{
    /// <summary>Free-text search against name/description.</summary>
    public string? Q { get; set; }

    public List<string>? Ingredients { get; set; }

    public string? CategoryId { get; set; }

    public string? DietType { get; set; }

    public int? MaxTotalTime { get; set; }

    /// <summary>"newest" (default), "name", or "totalTime".</summary>
    public string? Sort { get; set; }

    /// <summary>Opaque cursor returned as RecipePage.NextCursor from a previous page.</summary>
    public string? Cursor { get; set; }

    private int _pageSize = 20;

    /// <summary>Clamped to [1, 200]. A large value (e.g. 200) effectively disables paging for callers, like the wheel of fortune, that need the full filtered set at once.</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, 200);
    }
}

public sealed class RecipePage
{
    public IReadOnlyList<Recipe> Items { get; init; } = [];
    public string? NextCursor { get; init; }
}
