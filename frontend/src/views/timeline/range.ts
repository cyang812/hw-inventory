import type { ProjectSummaryDto, ProjectStatus } from '../../types/api';

const ONE_DAY_MS = 86_400_000;

export interface TimelineRange {
  startMs: number;
  endMs: number;
  totalDays: number;
}

export interface BarGeometry {
  leftPx: number;
  widthPx: number;
  isInferredStart: boolean;
  isOpenEnded: boolean;
  isUnscheduled: boolean;
}

// Status display order — active work first, archived/cold last.
export const STATUS_ORDER: ProjectStatus[] = [
  'inProgress',
  'planned',
  'paused',
  'idea',
  'done',
  'abandoned',
];

export const STATUS_COLORS: Record<ProjectStatus, string> = {
  inProgress: '#18a058',
  planned:    '#2080f0',
  paused:     '#f0a020',
  idea:       '#909399',
  done:       '#1f6f43',
  abandoned:  '#d03050',
};

export function statusColor(s: ProjectStatus): string {
  return STATUS_COLORS[s] ?? '#909399';
}

export function statusDashed(s: ProjectStatus): boolean {
  return s === 'paused' || s === 'abandoned';
}

// Effective start: prefer real startedAt, else fall back to createdAt.
export function effectiveStart(p: ProjectSummaryDto): { ms: number; inferred: boolean } | null {
  if (p.startedAt) return { ms: new Date(p.startedAt).getTime(), inferred: false };
  if (p.createdAt) return { ms: new Date(p.createdAt).getTime(), inferred: true };
  return null;
}

// Effective end: completedAt > targetDate > (today if status is active and there's a real start).
// Returns { ms, openEnded } where openEnded means there is no committed end yet.
export function effectiveEnd(p: ProjectSummaryDto, nowMs: number): { ms: number; openEnded: boolean } | null {
  if (p.completedAt) return { ms: new Date(p.completedAt).getTime(), openEnded: false };
  if (p.targetDate)  return { ms: new Date(p.targetDate).getTime(),  openEnded: false };
  if (p.startedAt && (p.status === 'inProgress' || p.status === 'planned' || p.status === 'paused'))
    return { ms: nowMs, openEnded: true };
  return null;
}

// A project is "unscheduled" when it has neither real startedAt nor any end date —
// we show it as a small marker at createdAt rather than as a long bar to today.
export function isUnscheduled(p: ProjectSummaryDto): boolean {
  return !p.startedAt && !p.targetDate && !p.completedAt;
}

export function computeRange(projects: ReadonlyArray<ProjectSummaryDto>, nowMs: number): TimelineRange {
  let minMs = Number.POSITIVE_INFINITY;
  let maxMs = Number.NEGATIVE_INFINITY;
  for (const p of projects) {
    const s = effectiveStart(p);
    const e = effectiveEnd(p, nowMs);
    if (s) minMs = Math.min(minMs, s.ms);
    if (e) maxMs = Math.max(maxMs, e.ms);
  }
  // Always include today.
  minMs = Math.min(minMs, nowMs);
  maxMs = Math.max(maxMs, nowMs);

  if (!Number.isFinite(minMs) || !Number.isFinite(maxMs)) {
    minMs = nowMs - 30 * ONE_DAY_MS;
    maxMs = nowMs + 30 * ONE_DAY_MS;
  }
  // Padding.
  minMs -= 7 * ONE_DAY_MS;
  maxMs += 14 * ONE_DAY_MS;
  // Guarantee at least 60 days visible so single-day projects don't compress to nothing.
  const MIN_SPAN = 60 * ONE_DAY_MS;
  if (maxMs - minMs < MIN_SPAN) {
    const center = (minMs + maxMs) / 2;
    minMs = center - MIN_SPAN / 2;
    maxMs = center + MIN_SPAN / 2;
  }
  return { startMs: minMs, endMs: maxMs, totalDays: Math.round((maxMs - minMs) / ONE_DAY_MS) };
}

export interface GeometryInputs {
  project: ProjectSummaryDto;
  range: TimelineRange;
  pxPerDay: number;
  nowMs: number;
}

export function barGeometry({ project, range, pxPerDay, nowMs }: GeometryInputs): BarGeometry | null {
  if (isUnscheduled(project)) {
    const created = project.createdAt ? new Date(project.createdAt).getTime() : nowMs;
    const left = ((created - range.startMs) / ONE_DAY_MS) * pxPerDay;
    return { leftPx: left, widthPx: 0, isInferredStart: true, isOpenEnded: false, isUnscheduled: true };
  }
  const s = effectiveStart(project);
  const e = effectiveEnd(project, nowMs);
  if (!s || !e) return null;

  const leftDays  = (s.ms - range.startMs) / ONE_DAY_MS;
  const widthDays = Math.max((e.ms - s.ms) / ONE_DAY_MS, 0);

  // Minimum visual width so a 1-day bar is still tappable.
  const widthPx = Math.max(widthDays * pxPerDay, 6);
  return {
    leftPx: leftDays * pxPerDay,
    widthPx,
    isInferredStart: s.inferred,
    isOpenEnded: e.openEnded,
    isUnscheduled: false,
  };
}

export function sortProjects(projects: ReadonlyArray<ProjectSummaryDto>): ProjectSummaryDto[] {
  const rank = (s: ProjectStatus) => {
    const i = STATUS_ORDER.indexOf(s);
    return i < 0 ? 99 : i;
  };
  const dateKey = (p: ProjectSummaryDto) => {
    const iso = p.startedAt ?? p.targetDate ?? p.createdAt;
    return iso ? new Date(iso).getTime() : 0;
  };
  return [...projects].sort((a, b) => {
    const r = rank(a.status) - rank(b.status);
    if (r !== 0) return r;
    return dateKey(a) - dateKey(b);
  });
}

// Month ticks for the header.
export function monthTicks(range: TimelineRange, pxPerDay: number): Array<{ label: string; leftPx: number }> {
  const ticks: Array<{ label: string; leftPx: number }> = [];
  const start = new Date(range.startMs);
  // Snap to first of month.
  const cursor = new Date(start.getFullYear(), start.getMonth(), 1);
  if (cursor.getTime() < range.startMs) cursor.setMonth(cursor.getMonth() + 1);
  while (cursor.getTime() <= range.endMs) {
    const leftDays = (cursor.getTime() - range.startMs) / ONE_DAY_MS;
    ticks.push({
      label: cursor.toLocaleDateString(undefined, { month: 'short', year: '2-digit' }),
      leftPx: leftDays * pxPerDay,
    });
    cursor.setMonth(cursor.getMonth() + 1);
  }
  return ticks;
}
