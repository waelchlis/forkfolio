import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDividerModule } from '@angular/material/divider';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { debounceTime, Subject, switchMap } from 'rxjs';
import { RecipeService, Recipe } from '../../services/recipe';
import { RecipeCardComponent } from '../recipe-card/recipe-card';
import { AuthService } from '../../services/auth';
import { CategoryService } from '../../services/category';
import { FavoriteService } from '../../services/favorite';
import { ConfirmDialogService } from '../../shared/confirm-dialog';
import { TIME_OPTIONS } from '../../shared/time-options';

@Component({
  selector: 'app-recipe-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatToolbarModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDividerModule,
    MatSnackBarModule,
    RecipeCardComponent,
  ],
  templateUrl: './recipe-list.html',
  styleUrl: './recipe-list.scss',
})
export class RecipeListComponent implements OnInit {
  searchQuery = signal<string>('');
  selectedIngredients = signal<string[]>([]);
  selectedCategory = signal<string>('');
  selectedDietType = signal<string[]>([]);
  maxTotalTime = signal<number>(0);
  sort = signal<'newest' | 'name' | 'totalTime'>('newest');
  showFavoritesOnly = signal<boolean>(false);

  loading = signal<boolean>(false);
  loadingMore = signal<boolean>(false);
  results = signal<Recipe[]>([]);
  nextCursor = signal<string | null>(null);

  readonly timeOptions = TIME_OPTIONS;

  private readonly queryChanges = new Subject<void>();

  allIngredients = computed(() => this.recipeService.allIngredients());
  allCategories = computed(() => this.categoryService.allCategories());

  displayedRecipes = computed(() => {
    if (!this.showFavoritesOnly()) {
      return this.results();
    }
    const favoriteIds = this.favoriteService.favoriteIds();
    return this.results().filter(r => favoriteIds.has(r.id));
  });

  constructor(
    protected recipeService: RecipeService,
    public authService: AuthService,
    protected categoryService: CategoryService,
    protected favoriteService: FavoriteService,
    private confirmDialog: ConfirmDialogService,
    private snackBar: MatSnackBar,
  ) {}

  ngOnInit(): void {
    this.categoryService.loadAll();

    this.queryChanges
      .pipe(
        debounceTime(250),
        switchMap(() => {
          this.loading.set(true);
          return this.recipeService.queryRecipes(this.buildQuery());
        })
      )
      .subscribe({
        next: page => {
          this.results.set(page.items);
          this.nextCursor.set(page.nextCursor);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });

    this.runQuery();
  }

  private buildQuery(cursor?: string) {
    return {
      q: this.searchQuery() || undefined,
      ingredients: this.selectedIngredients().length > 0 ? this.selectedIngredients() : undefined,
      categoryId: this.selectedCategory() || undefined,
      dietType: this.selectedDietType().length === 1 ? this.selectedDietType()[0] : undefined,
      maxTotalTime: this.maxTotalTime() || undefined,
      sort: this.sort(),
      cursor,
      pageSize: 24,
    };
  }

  runQuery(): void {
    this.queryChanges.next();
  }

  loadMore(): void {
    const cursor = this.nextCursor();
    if (!cursor || this.loadingMore()) return;

    this.loadingMore.set(true);
    this.recipeService.queryRecipes(this.buildQuery(cursor)).subscribe({
      next: page => {
        this.results.set([...this.results(), ...page.items]);
        this.nextCursor.set(page.nextCursor);
        this.loadingMore.set(false);
      },
      error: () => this.loadingMore.set(false),
    });
  }

  toggleDietType(type: 'vegan' | 'vegetarian'): void {
    const current = this.selectedDietType();
    this.selectedDietType.set(current.includes(type) ? current.filter(t => t !== type) : [...current, type]);
    this.runQuery();
  }

  toggleIngredient(ingredient: string): void {
    const current = this.selectedIngredients();
    this.selectedIngredients.set(current.includes(ingredient) ? current.filter(i => i !== ingredient) : [...current, ingredient]);
    this.runQuery();
  }

  clearFilters(): void {
    this.searchQuery.set('');
    this.selectedIngredients.set([]);
    this.selectedCategory.set('');
    this.selectedDietType.set([]);
    this.maxTotalTime.set(0);
    this.sort.set('newest');
    this.runQuery();
  }

  hasActiveFilters(): boolean {
    return !!(this.searchQuery() || this.selectedIngredients().length > 0 || this.selectedCategory() || this.selectedDietType().length || this.maxTotalTime());
  }

  toggleFavorite(recipeId: string): void {
    const recipe = this.results().find(r => r.id === recipeId);
    if (recipe) {
      this.favoriteService.toggle(recipe);
    }
  }

  async deleteRecipe(id: string): Promise<void> {
    if (!this.authService.isAuthenticated()) {
      return;
    }

    const confirmed = await this.confirmDialog.confirm({
      title: 'Delete recipe',
      message: 'Are you sure you want to delete this recipe? This cannot be undone.',
      confirmLabel: 'Delete',
      danger: true,
    });
    if (!confirmed) return;

    this.recipeService.deleteRecipe(id).subscribe(success => {
      if (success) {
        this.results.set(this.results().filter(r => r.id !== id));
        this.snackBar.open('Recipe deleted.', undefined, { duration: 2500 });
      } else {
        this.snackBar.open('Failed to delete recipe.', 'Dismiss', { duration: 4000 });
      }
    });
  }
}
