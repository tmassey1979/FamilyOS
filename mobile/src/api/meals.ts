import { api } from './client';

export type MealEntryDto = {
  id: string;
  dayUtc: string;
  slot: string;
  dishName: string;
  notes?: string | null;
  ingredientsCsv?: string | null;
};

export type MealPlanDto = {
  id: string;
  weekStartUtc: string;
  notes?: string | null;
  entries: MealEntryDto[];
};

export type RecipeDto = {
  id: string;
  name: string;
  servings: number;
  ingredientsCsv?: string | null;
  instructions?: string | null;
};

export const mealsApi = {
  week: (weekStart?: string) =>
    api<MealPlanDto>(`/api/meals/week${weekStart ? `?weekStart=${encodeURIComponent(weekStart)}` : ''}`),
  upsert: (body: {
    dayUtc: string;
    slot: string;
    dishName: string;
    notes?: string;
    ingredientsCsv?: string;
    weekStartUtc?: string;
  }) => api<MealEntryDto>('/api/meals/entries', { method: 'PUT', body: JSON.stringify(body) }),
  remove: (id: string) => api<void>(`/api/meals/entries/${id}`, { method: 'DELETE' }),
  toShopping: (weekStart?: string) =>
    api<{ added: number }>(
      `/api/meals/week/to-shopping${weekStart ? `?weekStart=${encodeURIComponent(weekStart)}` : ''}`,
      { method: 'POST' },
    ),
};

export const recipesApi = {
  list: () => api<RecipeDto[]>('/api/recipes'),
  create: (body: {
    name: string;
    servings?: number;
    ingredientsCsv?: string;
    instructions?: string;
  }) => api<RecipeDto>('/api/recipes', { method: 'POST', body: JSON.stringify(body) }),
  update: (
    id: string,
    body: { name: string; servings?: number; ingredientsCsv?: string; instructions?: string },
  ) => api<RecipeDto>(`/api/recipes/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  remove: (id: string) => api<void>(`/api/recipes/${id}`, { method: 'DELETE' }),
  apply: (id: string, body: { dayUtc: string; slot: string; weekStartUtc?: string }) =>
    api<MealEntryDto>(`/api/recipes/${id}/apply`, { method: 'POST', body: JSON.stringify(body) }),
  toShopping: (id: string) =>
    api<{ added: number }>(`/api/recipes/${id}/to-shopping`, { method: 'POST' }),
};
