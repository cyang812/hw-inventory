// Centralized date helpers for project planning fields (startedAt, targetDate, completedAt).
//
// These fields are conceptually *date-only* even though the backend stores them as
// DateTimeOffset. We serialize as noon UTC so the calendar day round-trips correctly
// across any timezone the user is likely to be in (±11h). That avoids the classic bug
// where 2026-05-24 selected in UTC+8 round-trips as 2026-05-23 because
// midnight-local-to-UTC crosses a day boundary.

const ONE_DAY_MS = 86_400_000;

export function toIsoNoonUtc(value: number | Date | null | undefined): string | null {
  if (value == null) return null;
  const d = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(d.getTime())) return null;
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}T12:00:00.000Z`;
}

// Parse an ISO string from the API into a local-midnight Date suitable for NDatePicker.
export function parseIsoToLocalMidnight(iso: string | null | undefined): number | null {
  if (!iso) return null;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return null;
  return new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
}

export function formatDate(iso: string | null | undefined, fallback = '—'): string {
  if (!iso) return fallback;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return fallback;
  return d.toLocaleDateString();
}

// Difference in whole days between two ISO timestamps (inclusive of start day).
// Returns null if either argument is missing or invalid.
export function diffDays(fromIso: string | null | undefined, toIso: string | null | undefined): number | null {
  if (!fromIso || !toIso) return null;
  const a = new Date(fromIso).getTime();
  const b = new Date(toIso).getTime();
  if (Number.isNaN(a) || Number.isNaN(b)) return null;
  return Math.round((b - a) / ONE_DAY_MS);
}

export function todayIsoNoonUtc(): string {
  return toIsoNoonUtc(new Date())!;
}
