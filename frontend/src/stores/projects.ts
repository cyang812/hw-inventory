import { defineStore } from 'pinia';
import { api } from '../api/client';
import type { ProjectSummaryDto, ProjectDto, ListResponse } from '../types/api';

export interface ProjectCreateBody {
  title: string;
  description?: string | null;
  status?: string;
  priority?: string;
  startedAt?: string | null;
  targetDate?: string | null;
  notes?: string | null;
}

export const useProjectsStore = defineStore('projects', {
  state: () => ({
    list: { items: [] as ProjectSummaryDto[], total: 0, limit: 50, offset: 0 } as ListResponse<ProjectSummaryDto>,
    detail: null as ProjectDto | null,
    loading: false,
    // Remember the last filters used by the current list view so that mutations can
    // refresh against the same view rather than resetting to defaults.
    lastFilters: {} as Record<string, unknown>,
  }),
  actions: {
    async fetchList(params: Record<string, unknown> = {}) {
      this.loading = true;
      try {
        this.lastFilters = { ...params };
        this.list = await api.get<ListResponse<ProjectSummaryDto>>('/api/projects', { limit: 100, ...params });
      } finally {
        this.loading = false;
      }
    },
    // Paginated fetch for the timeline view, where partial pages would silently
    // hide projects and corrupt the computed time range.
    async fetchAll(params: Record<string, unknown> = {}): Promise<ProjectSummaryDto[]> {
      const PAGE = 200;
      const out: ProjectSummaryDto[] = [];
      let offset = 0;
      let total = 0;
      do {
        const page = await api.get<ListResponse<ProjectSummaryDto>>('/api/projects', {
          ...params, limit: PAGE, offset,
        });
        out.push(...page.items);
        total = page.total;
        offset += page.items.length;
        if (page.items.length === 0) break;
      } while (offset < total);
      return out;
    },
    async fetchDetail(id: number) {
      this.loading = true;
      try {
        this.detail = await api.get<ProjectDto>(`/api/projects/${id}`, { include_archived: true });
      } finally {
        this.loading = false;
      }
    },
    async create(body: ProjectCreateBody): Promise<ProjectDto> {
      return await api.post<ProjectDto>('/api/projects', body);
    },
    async patch(id: number, ops: Array<{ op: string; path: string; value?: unknown }>) {
      await api.patch(`/api/projects/${id}`, ops);
      if (this.detail?.id === id) await this.fetchDetail(id);
      // Refresh with the filters used last so the list view doesn't snap back to defaults.
      await this.fetchList(this.lastFilters);
    },
    async archive(id: number) {
      await api.del(`/api/projects/${id}`);
      await this.fetchList(this.lastFilters);
    },
  },
});
