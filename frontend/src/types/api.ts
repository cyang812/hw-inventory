// Hand-written type subset that mirrors the backend DTOs. The plan calls for
// generating these from /openapi/v1.json via openapi-typescript at build time;
// keeping a hand-written shape here removes the runtime dependency and lets us
// move forward without booting the backend during a frontend-only checkout.
//
// When you want the generated version, run:
//   npx openapi-typescript http://127.0.0.1:5080/openapi/v1.json -o src/types/api.generated.d.ts
// and re-import.

export type HardwareStatus = 'available' | 'inUse' | 'loaned' | 'archived' | 'sold' | 'lost';
export type HardwareCondition = 'working' | 'partial' | 'broken' | 'unknown';
export type ProjectStatus = 'idea' | 'planned' | 'inProgress' | 'paused' | 'done' | 'abandoned';
export type ProjectPriority = 'low' | 'medium' | 'high';
export type ActivityKind = 'used' | 'flashed' | 'repaired' | 'measured' | 'configured' | 'inspected' | 'moved' | 'note';
export type HardwareConfigKind = 'firmware' | 'os' | 'bootloader' | 'config';

export interface ListResponse<T> {
  items: T[];
  total: number;
  limit: number;
  offset: number;
}

export interface CategoryDto {
  id: number;
  name: string;
  slug: string;
  parentId?: number | null;
  icon?: string | null;
  description?: string | null;
}

export interface TagDto {
  id: number;
  name: string;
  color?: string | null;
}

export interface HardwareSummaryDto {
  id: number;
  name: string;
  manufacturer?: string | null;
  model?: string | null;
  condition: HardwareCondition;
  status: HardwareStatus;
  location?: string | null;
  lastUsedAt?: string | null;
  lastActivityAt?: string | null;
  archivedAt?: string | null;
  categories: string[];
  tags: string[];
  createdAt: string;
  updatedAt: string;
}

export interface HardwareDto extends HardwareSummaryDto {
  serialNumber?: string | null;
  sku?: string | null;
  assetTag?: string | null;
  revision?: string | null;
  identifiers?: Record<string, unknown> | null;
  specs?: Record<string, unknown> | null;
  links?: Array<{ label: string; url: string; kind?: string }> | null;
  acquiredAt?: string | null;
  purchasedFrom?: string | null;
  purchaseUrl?: string | null;
  cost?: number | null;
  currency?: string | null;
  warrantyExpiresAt?: string | null;
  notes?: string | null;
  categories: any;
  tags: any;
  projects: Array<{ projectId: number; projectTitle: string; role?: string | null; createdAt: string }>;
  currentConfigs: HardwareConfigDto[];
  activeLoan?: LoanDto | null;
  recentActivities: ActivityDto[];
}

export interface ActivityDto {
  id: number;
  hardwareId: number;
  projectId?: number | null;
  kind: ActivityKind;
  description?: string | null;
  metadata?: Record<string, unknown> | null;
  occurredAt: string;
  createdAt: string;
}

export interface HardwareConfigDto {
  id: number;
  hardwareId: number;
  kind: HardwareConfigKind;
  name: string;
  version?: string | null;
  notes?: string | null;
  installedAt?: string | null;
  isCurrent: boolean;
  activityId?: number | null;
  createdAt: string;
}

export interface LoanDto {
  id: number;
  hardwareId: number;
  loanedTo: string;
  loanedAt: string;
  dueAt?: string | null;
  returnedAt?: string | null;
  notes?: string | null;
  createdAt: string;
}

export interface ProjectSummaryDto {
  id: number;
  title: string;
  slug: string;
  status: ProjectStatus;
  priority: ProjectPriority;
  startedAt?: string | null;
  targetDate?: string | null;
  completedAt?: string | null;
  archivedAt?: string | null;
  hardwareCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface ProjectDto extends ProjectSummaryDto {
  description?: string | null;
  notes?: string | null;
  links?: Array<{ label: string; url: string }> | null;
  hardware: Array<{ hardwareId: number; hardwareName: string; role?: string | null; createdAt: string }>;
}

export interface DashboardStatsDto {
  hardwareTotal: number;
  hardwareArchived: number;
  projectsTotal: number;
  activitiesLast30Days: number;
  idleCount: number;
  overdueLoans: number;
  byStatus: Record<string, number>;
  byCondition: Record<string, number>;
  byCategory: Record<string, number>;
  projectsByStatus: Record<string, number>;
}
