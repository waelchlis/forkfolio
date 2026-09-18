using System.Text;
using System.Text.Json;
using FoodHelper.Api.Contracts;
using FoodHelper.Api.Models;

namespace FoodHelper.Api.Services;

/// <summary>
/// Shared combined-filter/sort/cursor-paging logic used by both IRecipeStore implementations.
/// InMemoryRecipeStore runs it over the full in-memory collection; FirestoreRecipeStore pushes
/// what Firestore can do as plain equality Where clauses (categoryId, dietType — neither needs a
/// composite index) and runs the rest (free-text search, ingredient match, max-total-time,
/// sorting, cursor slicing) through this same engine over the resulting candidate set. Sharing
/// one engine keeps both stores' query behavior identical by construction.
/// </summary>
public static class RecipeQueryEngine
{
    public static RecipePage Apply(IEnumerable<Recipe> candidates, RecipeQueryParameters query)
    {
        IEnumerable<Recipe> filtered = candidates;

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var q = query.Q.Trim().ToLowerInvariant();
            filtered = filtered.Where(r =>
                r.Name.ToLowerInvariant().Contains(q) ||
                r.Description.ToLowerInvariant().Contains(q));
        }

        if (query.Ingredients is { Count: > 0 } ingredients)
        {
            var normalizedIngredients = ingredients.Select(i => i.Trim().ToLowerInvariant()).ToHashSet();
            filtered = filtered.Where(r => 
                normalizedIngredients.All(ing => 
                    r.Ingredients.Any(i => i.Name.Trim().ToLowerInvariant() == ing)));
        }

        if (!string.IsNullOrWhiteSpace(query.CategoryId))
        {
            filtered = filtered.Where(r => r.CategoryId == query.CategoryId);
        }

        if (!string.IsNullOrWhiteSpace(query.DietType))
        {
            filtered = filtered.Where(r => r.DietType == query.DietType);
        }

        if (query.MaxTotalTime is { } maxTotalTime && maxTotalTime > 0)
        {
            filtered = filtered.Where(r => r.PrepTime + r.CookTime <= maxTotalTime);
        }

        var sort = (query.Sort ?? "newest").Trim().ToLowerInvariant();
        List<Recipe> ordered = sort switch
        {
            "name" => filtered.OrderBy(r => r.Name.ToLowerInvariant()).ThenBy(r => r.Id).ToList(),
            "totaltime" => filtered.OrderBy(r => r.PrepTime + r.CookTime).ThenBy(r => r.Id).ToList(),
            _ => filtered.OrderByDescending(r => r.UpdatedAt).ThenBy(r => r.Id).ToList(),
        };

        var startIndex = 0;
        if (!string.IsNullOrWhiteSpace(query.Cursor))
        {
            var cursorId = DecodeCursor(query.Cursor);
            if (cursorId is not null)
            {
                var idx = ordered.FindIndex(r => r.Id == cursorId);
                startIndex = idx >= 0 ? idx + 1 : 0;
            }
        }

        var page = ordered.Skip(startIndex).Take(query.PageSize).ToList();
        var hasMore = startIndex + page.Count < ordered.Count;
        var nextCursor = hasMore && page.Count > 0 ? EncodeCursor(page[^1].Id) : null;

        return new RecipePage { Items = page, NextCursor = nextCursor };
    }

    private static string EncodeCursor(string id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new CursorToken { Id = id })));

    private static string? DecodeCursor(string cursor)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return JsonSerializer.Deserialize<CursorToken>(json)?.Id;
        }
        catch
        {
            return null;
        }
    }

    private sealed class CursorToken
    {
        public string Id { get; set; } = string.Empty;
    }
}
