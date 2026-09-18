import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface Ingredient {
  id: string;
  name: string;
  amount: number;
  unit: string;
}

export interface Recipe {
  id: string;
  name: string;
  description: string;
  servings: number;
  prepTime: number;
  cookTime: number;
  ingredients: Ingredient[];
  instructions: string[];
  tips: string[];
  images: string[];
  categoryId?: string;
  dietType?: 'vegan' | 'vegetarian';
  note?: string;
  creatorName?: string;
  createdAt: Date;
  updatedAt: Date;
}

export interface RecipeRevision {
  id: string;
  recipeId: string;
  snapshot: Recipe;
  editorSubject: string;
  editorName: string;
  createdAt: string;
}

export interface RecipeImportResult {
  name: string;
  success: boolean;
  id?: string;
  error?: string;
}

export interface RecipeQuery {
  q?: string;
  ingredients?: string[];
  categoryId?: string;
  dietType?: string;
  maxTotalTime?: number;
  sort?: 'newest' | 'name' | 'totalTime';
  cursor?: string;
  pageSize?: number;
}

export interface RecipePage {
  items: Recipe[];
  nextCursor: string | null;
}

type RecipeDto = Omit<Recipe, 'createdAt' | 'updatedAt'> & {
  createdAt: string;
  updatedAt: string;
};

type RecipePageDto = {
  items: RecipeDto[];
  nextCursor: string | null;
};

@Injectable({
  providedIn: 'root',
})
export class RecipeService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/recipes`;
  private recipes = signal<Recipe[]>([]);

  public readonly allRecipes = this.recipes.asReadonly();

  public readonly allIngredients = computed(() => {
    const ingredients = new Set<string>();
    this.recipes().forEach(recipe => {
      recipe.ingredients.forEach(ing => {
        ingredients.add(ing.name);
      });
    });
    return Array.from(ingredients).sort();
  });

  constructor() {}

  /** Loads the full recipe collection into the shared in-memory cache used by the wheel of fortune, the add-meal dialog, etc. */
  refreshRecipes(): Observable<Recipe[]> {
    return this.queryRecipes({ pageSize: 200 }).pipe(
      map(page => page.items),
      tap(items => this.recipes.set(items)),
      catchError(() => of(this.recipes()))
    );
  }

  /** Server-side combined filter + sort + cursor pagination, used directly by the recipe list search UI. */
  queryRecipes(query: RecipeQuery): Observable<RecipePage> {
    let params: Record<string, string | string[]> = {};
    if (query.q) params['q'] = query.q;
    if (query.ingredients && query.ingredients.length > 0) {
      params['Ingredients'] = query.ingredients;
    }
    if (query.categoryId) params['categoryId'] = query.categoryId;
    if (query.dietType) params['dietType'] = query.dietType;
    if (query.maxTotalTime) params['maxTotalTime'] = String(query.maxTotalTime);
    if (query.sort) params['sort'] = query.sort;
    if (query.cursor) params['cursor'] = query.cursor;
    if (query.pageSize) params['pageSize'] = String(query.pageSize);

    return this.http.get<RecipePageDto>(this.apiUrl, { params }).pipe(
      map(page => ({
        items: page.items.map(item => this.fromDto(item)),
        nextCursor: page.nextCursor,
      }))
    );
  }

  getRecipes(): Recipe[] {
    return this.recipes();
  }

  getRecipeById(id: string): Recipe | undefined {
    return this.recipes().find(r => r.id === id);
  }

  loadRecipeById(id: string): Observable<Recipe | undefined> {
    return this.http.get<RecipeDto>(`${this.apiUrl}/${id}`).pipe(
      map(item => this.fromDto(item)),
      tap(recipe => {
        const next = this.upsertInMemory(this.recipes(), recipe);
        this.recipes.set(next);
      }),
      catchError(() => of(this.getRecipeById(id)))
    );
  }

  getSimilar(id: string): Observable<Recipe[]> {
    return this.http.get<RecipeDto[]>(`${this.apiUrl}/${id}/similar`).pipe(
      map(items => items.map(item => this.fromDto(item))),
      catchError(() => of([]))
    );
  }

  getHistory(id: string): Observable<RecipeRevision[]> {
    return this.http.get<RecipeRevision[]>(`${this.apiUrl}/${id}/history`);
  }

  createRecipe(recipe: Omit<Recipe, 'id' | 'createdAt' | 'updatedAt'>): Observable<Recipe> {
    return this.http.post<RecipeDto>(this.apiUrl, recipe).pipe(
      map(item => this.fromDto(item)),
      tap(created => this.recipes.set([...this.recipes(), created]))
    );
  }

  updateRecipe(id: string, updates: Partial<Omit<Recipe, 'id' | 'createdAt'>>): Observable<Recipe | undefined> {
    const existing = this.getRecipeById(id);
    if (!existing) {
      return of(undefined);
    }

    const payload = {
      name: (updates.name ?? existing.name).trim(),
      description: (updates.description ?? existing.description).trim(),
      servings: updates.servings ?? existing.servings,
      prepTime: updates.prepTime ?? existing.prepTime,
      cookTime: updates.cookTime ?? existing.cookTime,
      ingredients: updates.ingredients ?? existing.ingredients,
      instructions: updates.instructions ?? existing.instructions,
      tips: updates.tips ?? existing.tips,
      images: updates.images ?? existing.images,
      categoryId: updates.categoryId ?? existing.categoryId,
      dietType: 'dietType' in updates ? updates.dietType : existing.dietType,
      note: 'note' in updates ? updates.note : existing.note,
    };

    return this.http.put<RecipeDto>(`${this.apiUrl}/${id}`, payload).pipe(
      map(item => this.fromDto(item)),
      tap(saved => this.recipes.set(this.upsertInMemory(this.recipes(), saved))),
      map(saved => saved)
    );
  }

  deleteRecipe(id: string): Observable<boolean> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`).pipe(
      map(() => true),
      tap(() => this.recipes.set(this.recipes().filter(r => r.id !== id))),
      catchError(() => of(false))
    );
  }

  uploadImage(recipeId: string, file: File): Observable<Recipe> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<RecipeDto>(`${this.apiUrl}/${recipeId}/image`, formData).pipe(
      map(item => this.fromDto(item)),
      tap(saved => this.recipes.set(this.upsertInMemory(this.recipes(), saved)))
    );
  }

  removeImage(recipeId: string, url: string): Observable<Recipe> {
    return this.http.request<RecipeDto>('DELETE', `${this.apiUrl}/${recipeId}/images`, { body: { url } }).pipe(
      map(item => this.fromDto(item)),
      tap(saved => this.recipes.set(this.upsertInMemory(this.recipes(), saved)))
    );
  }

  reorderImages(recipeId: string, images: string[]): Observable<Recipe> {
    return this.http.put<RecipeDto>(`${this.apiUrl}/${recipeId}/images/order`, { images }).pipe(
      map(item => this.fromDto(item)),
      tap(saved => this.recipes.set(this.upsertInMemory(this.recipes(), saved)))
    );
  }

  exportRecipes(): Observable<Recipe[]> {
    return this.http.get<RecipeDto[]>(`${this.apiUrl}/export`).pipe(map(items => items.map(item => this.fromDto(item))));
  }

  importRecipes(items: Omit<Recipe, 'id' | 'createdAt' | 'updatedAt' | 'creatorName'>[]): Observable<RecipeImportResult[]> {
    return this.http.post<RecipeImportResult[]>(`${this.apiUrl}/import`, items);
  }

  scaleIngredients(recipe: Recipe, servings: number): Ingredient[] {
    const scale = servings / recipe.servings;
    return recipe.ingredients.map(ing => ({
      ...ing,
      amount: ing.amount * scale,
    }));
  }

  private fromDto(recipe: RecipeDto): Recipe {
    return {
      ...recipe,
      images: recipe.images ?? [],
      createdAt: new Date(recipe.createdAt),
      updatedAt: new Date(recipe.updatedAt),
    };
  }

  private upsertInMemory(recipes: Recipe[], recipe: Recipe): Recipe[] {
    const existing = recipes.some(item => item.id === recipe.id);
    if (!existing) {
      return [...recipes, recipe];
    }

    return recipes.map(item => (item.id === recipe.id ? recipe : item));
  }
}
