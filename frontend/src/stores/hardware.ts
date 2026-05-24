import { defineStore } from 'pinia';
import { api } from '../api/client';
import type {
  HardwareSummaryDto, HardwareDto, ListResponse, ActivityDto,
} from '../types/api';

interface HardwareListFilter {
  q?: string;
  category?: number[];
  tag?: number[];
  status?: string;
  condition?: string;
  idle_days?: number;
  include_archived?: boolean;
  limit?: number;
  offset?: number;
  sort?: string;
}

export const useHardwareStore = defineStore('hardware', {
  state: () => ({
    list: { items: [] as HardwareSummaryDto[], total: 0, limit: 50, offset: 0 } as ListResponse<HardwareSummaryDto>,
    detail: null as HardwareDto | null,
    loading: false,
    filter: { limit: 50, offset: 0, sort: 'name' } as HardwareListFilter,
  }),
  actions: {
    async fetchList(merge: Partial<HardwareListFilter> = {}) {
      this.loading = true;
      try {
        this.filter = { ...this.filter, ...merge };
        this.list = await api.get<ListResponse<HardwareSummaryDto>>('/api/hardware', this.filter);
      } finally {
        this.loading = false;
      }
    },
    async fetchDetail(id: number) {
      this.loading = true;
      try {
        this.detail = await api.get<HardwareDto>(`/api/hardware/${id}`, { include_archived: true });
      } finally {
        this.loading = false;
      }
    },
    async markUsedToday(id: number): Promise<ActivityDto> {
      const a = await api.post<ActivityDto>(`/api/hardware/${id}/mark-used-today`);
      if (this.detail?.id === id) await this.fetchDetail(id);
      await this.fetchList();
      return a;
    },
    async archive(id: number) {
      await api.del(`/api/hardware/${id}`);
      await this.fetchList();
    },
    async unarchive(id: number) {
      await api.post(`/api/hardware/${id}/unarchive`);
      await this.fetchList();
    },
    async create(body: Record<string, unknown>): Promise<HardwareDto> {
      return await api.post<HardwareDto>('/api/hardware', body);
    },
    async logActivity(id: number, body: { kind: string; description?: string; occurredAt?: string; projectId?: number }) {
      await api.post(`/api/hardware/${id}/activities`, body);
      if (this.detail?.id === id) await this.fetchDetail(id);
    },
    async patch(id: number, ops: Array<{ op: string; path: string; value?: unknown }>) {
      await api.patch<HardwareDto>(`/api/hardware/${id}`, ops);
      if (this.detail?.id === id) await this.fetchDetail(id);
    },
    async linkProject(id: number, projectId: number, role?: string) {
      await api.put(`/api/hardware/${id}/projects/${projectId}`, { role });
      if (this.detail?.id === id) await this.fetchDetail(id);
    },
    async unlinkProject(id: number, projectId: number) {
      await api.del(`/api/hardware/${id}/projects/${projectId}`);
      if (this.detail?.id === id) await this.fetchDetail(id);
    },
    async recordConfig(id: number, body: { kind: string; name: string; version?: string; notes?: string; installedAt?: string; isCurrent?: boolean }) {
      await api.post(`/api/hardware/${id}/configs`, body);
      if (this.detail?.id === id) await this.fetchDetail(id);
    },
    async startLoan(id: number, body: { loanedTo: string; dueAt?: string; notes?: string }) {
      await api.post(`/api/hardware/${id}/loans`, body);
      if (this.detail?.id === id) await this.fetchDetail(id);
    },
    async returnLoan(id: number, loanId: number) {
      await api.patch(`/api/hardware/${id}/loans/${loanId}`, [{ op: 'replace', path: '/returnedAt', value: new Date().toISOString() }]);
      if (this.detail?.id === id) await this.fetchDetail(id);
    },
  },
});
