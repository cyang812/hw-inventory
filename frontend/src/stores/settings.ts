import { defineStore } from 'pinia';
import { api } from '../api/client';
import type { CategoryDto, TagDto, ListResponse } from '../types/api';

export const useSettingsStore = defineStore('settings', {
  state: () => ({
    categories: [] as CategoryDto[],
    tags: [] as TagDto[],
  }),
  actions: {
    async fetchCategories() {
      const r = await api.get<ListResponse<CategoryDto>>('/api/categories', { limit: 200 });
      this.categories = r.items;
    },
    async fetchTags() {
      const r = await api.get<ListResponse<TagDto>>('/api/tags', { limit: 200 });
      this.tags = r.items;
    },
    async createCategory(body: { name: string; slug?: string; description?: string }) {
      await api.post('/api/categories', body);
      await this.fetchCategories();
    },
    async deleteCategory(id: number) {
      await api.del(`/api/categories/${id}`);
      await this.fetchCategories();
    },
    async createTag(body: { name: string; color?: string }) {
      await api.post('/api/tags', body);
      await this.fetchTags();
    },
    async deleteTag(id: number) {
      await api.del(`/api/tags/${id}`);
      await this.fetchTags();
    },
  },
});
