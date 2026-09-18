using FoodHelper.Api.Contracts;
using FoodHelper.Api.Models;
using FoodHelper.Api.Services;

namespace FoodHelper.Api.Tests.Stores;

/// <summary>
/// Behavior every IRecipeStore implementation must satisfy. Run against InMemoryRecipeStore for
/// now; a FirestoreRecipeStoreContractTests subclass (against a Firestore emulator) can be added
/// later with no changes needed here — see planning/epic-nf1-backend-testing-foundation.md.
/// </summary>
public abstract class RecipeStoreContractTests
{
    protected abstract IRecipeStore CreateStore();

    private static Recipe MakeRecipe(string id, string name, int prepTime = 10, int cookTime = 10, string? categoryId = null, string? dietType = null, DateTime? updatedAt = null) => new()
    {
        Id = id,
        Name = name,
        Description = $"Description for {name}",
        Servings = 4,
        PrepTime = prepTime,
        CookTime = cookTime,
        Ingredients = [new RecipeIngredient { Id = "i1", Name = "Flour", Amount = 200, Unit = "g" }],
        Instructions = ["Mix", "Bake"],
        CategoryId = categoryId,
        DietType = dietType,
        CreatorName = "Tester",
        CreatedAt = updatedAt ?? DateTime.UtcNow,
        UpdatedAt = updatedAt ?? DateTime.UtcNow,
    };

    [Fact]
    public async Task UpsertAndGetById_RoundTrips()
    {
        var store = CreateStore();
        var recipe = MakeRecipe("r1", "Pancakes");

        await store.UpsertAsync(recipe, CancellationToken.None);
        var fetched = await store.GetByIdAsync("r1", CancellationToken.None);

        Assert.NotNull(fetched);
        Assert.Equal("Pancakes", fetched!.Name);
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenNotFound()
    {
        var store = CreateStore();

        var fetched = await store.GetByIdAsync("missing", CancellationToken.None);

        Assert.Null(fetched);
    }

    [Fact]
    public async Task Delete_RemovesRecipe_AndReturnsTrue()
    {
        var store = CreateStore();
        await store.UpsertAsync(MakeRecipe("r1", "Pancakes"), CancellationToken.None);

        var deleted = await store.DeleteAsync("r1", CancellationToken.None);
        var fetched = await store.GetByIdAsync("r1", CancellationToken.None);

        Assert.True(deleted);
        Assert.Null(fetched);
    }

    [Fact]
    public async Task Delete_ReturnsFalse_WhenRecipeDoesNotExist()
    {
        var store = CreateStore();

        var deleted = await store.DeleteAsync("missing", CancellationToken.None);

        Assert.False(deleted);
    }

    [Fact]
    public async Task GetAll_OrdersByUpdatedAtDescending()
    {
        var store = CreateStore();
        var now = DateTime.UtcNow;
        await store.UpsertAsync(MakeRecipe("r1", "Oldest", updatedAt: now.AddMinutes(-10)), CancellationToken.None);
        await store.UpsertAsync(MakeRecipe("r2", "Newest", updatedAt: now), CancellationToken.None);
        await store.UpsertAsync(MakeRecipe("r3", "Middle", updatedAt: now.AddMinutes(-5)), CancellationToken.None);

        var all = await store.GetAllAsync(CancellationToken.None);

        Assert.Equal(["Newest", "Middle", "Oldest"], all.Select(r => r.Name));
    }

    [Fact]
    public async Task QueryAsync_CombinesFiltersWithAndSemantics()
    {
        var store = CreateStore();
        await store.UpsertAsync(MakeRecipe("r1", "Vegan Curry", categoryId: "cat-dinner", dietType: "vegan"), CancellationToken.None);
        await store.UpsertAsync(MakeRecipe("r2", "Vegan Salad", categoryId: "cat-lunch", dietType: "vegan"), CancellationToken.None);
        await store.UpsertAsync(MakeRecipe("r3", "Meat Curry", categoryId: "cat-dinner", dietType: null), CancellationToken.None);

        var page = await store.QueryAsync(new RecipeQueryParameters { CategoryId = "cat-dinner", DietType = "vegan" }, CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal("r1", page.Items[0].Id);
    }

    [Fact]
    public async Task QueryAsync_TextSearch_MatchesNameOrDescriptionCaseInsensitively()
    {
        var store = CreateStore();
        await store.UpsertAsync(MakeRecipe("r1", "Chocolate Cake"), CancellationToken.None);
        await store.UpsertAsync(MakeRecipe("r2", "Vanilla Cake"), CancellationToken.None);

        var page = await store.QueryAsync(new RecipeQueryParameters { Q = "choco" }, CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal("r1", page.Items[0].Id);
    }

    [Fact]
    public async Task QueryAsync_MaxTotalTime_FiltersOnPrepPlusCookTime()
    {
        var store = CreateStore();
        await store.UpsertAsync(MakeRecipe("r1", "Quick", prepTime: 5, cookTime: 10), CancellationToken.None);
        await store.UpsertAsync(MakeRecipe("r2", "Slow", prepTime: 30, cookTime: 60), CancellationToken.None);

        var page = await store.QueryAsync(new RecipeQueryParameters { MaxTotalTime = 20 }, CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal("r1", page.Items[0].Id);
    }

    [Fact]
    public async Task QueryAsync_SortByName_OrdersAlphabetically()
    {
        var store = CreateStore();
        await store.UpsertAsync(MakeRecipe("r1", "Zebra Cake"), CancellationToken.None);
        await store.UpsertAsync(MakeRecipe("r2", "Apple Pie"), CancellationToken.None);

        var page = await store.QueryAsync(new RecipeQueryParameters { Sort = "name" }, CancellationToken.None);

        Assert.Equal(["Apple Pie", "Zebra Cake"], page.Items.Select(r => r.Name));
    }

    [Fact]
    public async Task QueryAsync_Paginates_UsingCursor()
    {
        var store = CreateStore();
        for (var i = 0; i < 5; i++)
        {
            await store.UpsertAsync(MakeRecipe($"r{i}", $"Recipe {i}"), CancellationToken.None);
        }

        var firstPage = await store.QueryAsync(new RecipeQueryParameters { Sort = "name", PageSize = 2 }, CancellationToken.None);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.NotNull(firstPage.NextCursor);

        var secondPage = await store.QueryAsync(new RecipeQueryParameters { Sort = "name", PageSize = 2, Cursor = firstPage.NextCursor }, CancellationToken.None);
        Assert.Equal(2, secondPage.Items.Count);

        // No overlap between pages.
        Assert.Empty(firstPage.Items.Select(r => r.Id).Intersect(secondPage.Items.Select(r => r.Id)));
    }

    [Fact]
    public async Task QueryAsync_NextCursor_IsNullOnLastPage()
    {
        var store = CreateStore();
        await store.UpsertAsync(MakeRecipe("r1", "Only Recipe"), CancellationToken.None);

        var page = await store.QueryAsync(new RecipeQueryParameters { PageSize = 20 }, CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Null(page.NextCursor);
    }
}

public class InMemoryRecipeStoreTests : RecipeStoreContractTests
{
    protected override IRecipeStore CreateStore() => new InMemoryRecipeStore(useSeedData: false);
}
