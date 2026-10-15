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
