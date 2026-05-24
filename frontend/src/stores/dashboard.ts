import { defineStore } from 'pinia';
import { api } from '../api/client';
import type { DashboardStatsDto, HardwareSummaryDto, ListResponse, LoanDto } from '../types/api';

export const useDashboardStore = defineStore('dashboard', {
  state: () => ({
    stats: null as DashboardStatsDto | null,
    idle: [] as HardwareSummaryDto[],
    suggestions: [] as HardwareSummaryDto[],
    overdue: [] as LoanDto[],
  }),
  actions: {
    async refresh() {
      const [stats, idle, suggestions, overdue] = await Promise.all([
        api.get<DashboardStatsDto>('/api/dashboard/stats'),
        api.get<ListResponse<HardwareSummaryDto>>('/api/dashboard/idle', { limit: 100 }),
        api.get<ListResponse<HardwareSummaryDto>>('/api/dashboard/suggestions', { limit: 100 }),
        api.get<ListResponse<LoanDto>>('/api/dashboard/overdue-loans'),
      ]);
      this.stats = stats;
      this.idle = idle.items;
      this.suggestions = suggestions.items;
      this.overdue = overdue.items;
    },
  },
});
