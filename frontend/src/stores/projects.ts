import { defineStore } from 'pinia';
import { api } from '../api/client';
import type { ProjectSummaryDto, ProjectDto, ListResponse } from '../types/api';

export const useProjectsStore = defineStore('projects', {
  state: () => ({
    list: { items: [] as ProjectSummaryDto[], total: 0, limit: 50, offset: 0 } as ListResponse<ProjectSummaryDto>,
    detail: null as ProjectDto | null,
    loading: false,
  }),
  actions: {
    async fetchList(params: Record<string, unknown> = {}) {
      this.loading = true;
      try {
        this.list = await api.get<ListResponse<ProjectSummaryDto>>('/api/projects', { limit: 100, ...params });
      } finally {
        this.loading = false;
      }
    },
    async fetchDetail(id: number) {
      this.loading = true;
      try {
        this.detail = await api.get<ProjectDto>(`/api/projects/${id}`, { include_archived: true });
      } finally {
        this.loading = false;
      }
    },
    async create(body: { title: string; description?: string; status?: string; priority?: string }): Promise<ProjectDto> {
      return await api.post<ProjectDto>('/api/projects', body);
    },
    async patch(id: number, ops: Array<{ op: string; path: string; value?: unknown }>) {
      await api.patch(`/api/projects/${id}`, ops);
      if (this.detail?.id === id) await this.fetchDetail(id);
      await this.fetchList();
    },
    async archive(id: number) {
      await api.del(`/api/projects/${id}`);
      await this.fetchList();
    },
  },
});
