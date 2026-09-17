using System.Collections.Concurrent;
using FoodHelper.Api.Contracts;
using FoodHelper.Api.Models;

namespace FoodHelper.Api.Services;

public sealed class InMemoryRecipeStore : IRecipeStore
{
    private readonly ConcurrentDictionary<string, Recipe> _recipes = new();
    private readonly bool _useSeedData;

    public InMemoryRecipeStore(bool useSeedData = true)
    {
        _useSeedData = useSeedData;
        if (_useSeedData)
        {
            SeedInitialData();
        }
    }

    private void SeedInitialData()
    {
        var now = DateTime.UtcNow;
        var recipes = new List<Recipe>
        {
            new Recipe
            {
                Id = "seed-1",
                Name = "Spaghetti Bolognese",
                Description = "Classic Italian pasta dish with rich meat sauce",
                Servings = 4,
                PrepTime = 20,
                CookTime = 45,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient { Id = "i1", Name = "Ground beef", Amount = 500, Unit = "g" },
                    new RecipeIngredient { Id = "i2", Name = "Spaghetti", Amount = 400, Unit = "g" },
                    new RecipeIngredient { Id = "i3", Name = "Tomato sauce", Amount = 400, Unit = "ml" },
                    new RecipeIngredient { Id = "i4", Name = "Onion", Amount = 1, Unit = "" },
                    new RecipeIngredient { Id = "i5", Name = "Garlic", Amount = 2, Unit = "cloves" },
                },
                Instructions = new List<string>
                {
                    "Brown the ground beef in a large pan",
                    "Add chopped onions and garlic",
                    "Pour in tomato sauce and simmer for 30 minutes",
                    "Cook spaghetti according to package instructions",
                    "Combine and serve hot"
                },
                Tips = new List<string> { "Add a pinch of sugar to balance the tomato acidity", "Use fresh Parmesan cheese" },
                Images = new List<string>(),
                CategoryId = "cat-dinner",
                DietType = null,
                CreatorName = "Seed Data",
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now.AddDays(-5),
            },
            new Recipe
            {
                Id = "seed-2",
                Name = "Chicken Stir Fry",
                Description = "Quick and healthy chicken stir fry with vegetables",
                Servings = 2,
                PrepTime = 15,
                CookTime = 10,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient { Id = "i1", Name = "Chicken breast", Amount = 300, Unit = "g" },
                    new RecipeIngredient { Id = "i2", Name = "Bell pepper", Amount = 1, Unit = "" },
                    new RecipeIngredient { Id = "i3", Name = "Broccoli", Amount = 200, Unit = "g" },
                    new RecipeIngredient { Id = "i4", Name = "Soy sauce", Amount = 30, Unit = "ml" },
                    new RecipeIngredient { Id = "i5", Name = "Garlic", Amount = 1, Unit = "clove" },
                    new RecipeIngredient { Id = "i6", Name = "Ginger", Amount = 1, Unit = "tbsp" },
                },
                Instructions = new List<string>
                {
                    "Cut chicken into strips and marinate with soy sauce",
                    "Chop all vegetables",
                    "Stir fry chicken until cooked through",
                    "Add vegetables and cook until tender",
                    "Serve hot with rice"
                },
                Tips = new List<string> { "Use high heat for best results", "Add a splash of sesame oil at the end" },
                Images = new List<string>(),
                CategoryId = "cat-dinner",
                DietType = null,
                CreatorName = "Seed Data",
                CreatedAt = now.AddDays(-4),
                UpdatedAt = now.AddDays(-4),
            },
            new Recipe
            {
                Id = "seed-3",
                Name = "Vegetable Lasagna",
                Description = "Hearty vegetable lasagna with rich cheese layers",
                Servings = 6,
                PrepTime = 30,
                CookTime = 45,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient { Id = "i1", Name = "Lasagna sheets", Amount = 12, Unit = "" },
                    new RecipeIngredient { Id = "i2", Name = "Zucchini", Amount = 2, Unit = "" },
                    new RecipeIngredient { Id = "i3", Name = "Eggplant", Amount = 1, Unit = "" },
                    new RecipeIngredient { Id = "i4", Name = "Ricotta cheese", Amount = 500, Unit = "g" },
                    new RecipeIngredient { Id = "i5", Name = "Mozzarella", Amount = 300, Unit = "g" },
                    new RecipeIngredient { Id = "i6", Name = "Tomato sauce", Amount = 500, Unit = "ml" },
                },
                Instructions = new List<string>
                {
                    "Preheat oven to 180°C",
                    "Layer lasagna sheets, vegetables, and cheeses",
                    "Repeat layers until all ingredients are used",
                    "Top with tomato sauce and mozzarella",
                    "Bake for 45 minutes"
                },
                Tips = new List<string> { "Let it rest for 10 minutes before serving", "Add spinach for extra nutrition" },
                Images = new List<string>(),
                CategoryId = "cat-dinner",
                DietType = "vegetarian",
                CreatorName = "Seed Data",
                CreatedAt = now.AddDays(-3),
                UpdatedAt = now.AddDays(-3),
            },
            new Recipe
            {
                Id = "seed-4",
                Name = "Beef Burger",
                Description = "Juicy beef burger with all the classic toppings",
                Servings = 1,
                PrepTime = 10,
                CookTime = 10,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient { Id = "i1", Name = "Ground beef", Amount = 200, Unit = "g" },
                    new RecipeIngredient { Id = "i2", Name = "Burger bun", Amount = 1, Unit = "" },
                    new RecipeIngredient { Id = "i3", Name = "Lettuce", Amount = 1, Unit = "leaf" },
                    new RecipeIngredient { Id = "i4", Name = "Tomato", Amount = 2, Unit = "slices" },
                    new RecipeIngredient { Id = "i5", Name = "Cheese", Amount = 1, Unit = "slice" },
                    new RecipeIngredient { Id = "i6", Name = "Onion", Amount = 2, Unit = "slices" },
                },
                Instructions = new List<string>
                {
                    "Shape ground beef into patties",
                    "Cook patties on medium-high heat for 4 minutes per side",
                    "Toast burger buns",
                    "Assemble with lettuce, tomato, cheese, and onion",
                    "Serve immediately"
                },
                Tips = new List<string> { "Don't press the patty while cooking", "Use brioche buns for extra flavor" },
                Images = new List<string>(),
                CategoryId = "cat-lunch",
                DietType = null,
                CreatorName = "Seed Data",
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now.AddDays(-2),
            },
            new Recipe
            {
                Id = "seed-5",
                Name = "Tomato Soup",
                Description = "Classic creamy tomato soup, perfect for any meal",
                Servings = 4,
                PrepTime = 10,
                CookTime = 25,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient { Id = "i1", Name = "Tomatoes", Amount = 8, Unit = "" },
                    new RecipeIngredient { Id = "i2", Name = "Onion", Amount = 1, Unit = "" },
                    new RecipeIngredient { Id = "i3", Name = "Garlic", Amount = 2, Unit = "cloves" },
                    new RecipeIngredient { Id = "i4", Name = "Vegetable stock", Amount = 500, Unit = "ml" },
                    new RecipeIngredient { Id = "i5", Name = "Cream", Amount = 100, Unit = "ml" },
                    new RecipeIngredient { Id = "i6", Name = "Basil", Amount = 5, Unit = "leaves" },
                },
                Instructions = new List<string>
                {
                    "Sauté onions and garlic until soft",
                    "Add chopped tomatoes and cook for 10 minutes",
                    "Add vegetable stock and simmer for 15 minutes",
                    "Blend until smooth",
                    "Stir in cream and basil",
                    "Heat through and serve"
                },
                Tips = new List<string> { "Add a pinch of sugar to enhance the tomato flavor", "Serve with crusty bread" },
                Images = new List<string>(),
                CategoryId = "cat-soup",
                DietType = "vegetarian",
                CreatorName = "Seed Data",
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddDays(-1),
            }
        };

        foreach (var recipe in recipes)
        {
            _recipes[recipe.Id] = recipe;
        }
    }

    public Task<IReadOnlyList<Recipe>> GetAllAsync(CancellationToken cancellationToken)
    {
        var items = _recipes.Values
            .OrderByDescending(r => r.UpdatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<Recipe>>(items);
    }

    public Task<Recipe?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        _recipes.TryGetValue(id, out var recipe);
        return Task.FromResult(recipe);
    }

    public Task<Recipe> UpsertAsync(Recipe recipe, CancellationToken cancellationToken)
    {
        _recipes[recipe.Id] = recipe;
        return Task.FromResult(recipe);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_recipes.TryRemove(id, out _));
    }

    public Task<RecipePage> QueryAsync(RecipeQueryParameters query, CancellationToken cancellationToken)
    {
        return Task.FromResult(RecipeQueryEngine.Apply(_recipes.Values, query));
    }
}
