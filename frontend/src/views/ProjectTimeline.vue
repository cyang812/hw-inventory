<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import {
  NCard, NSpace, NSelect, NSwitch, NSpin, NEmpty, NTag, NTooltip, NButton,
  useMessage,
} from 'naive-ui';
import { useProjectsStore } from '../stores/projects';
import type { ProjectSummaryDto, ProjectStatus, ProjectPriority } from '../types/api';
import {
  computeRange, sortProjects, barGeometry, monthTicks,
  statusColor, statusDashed, STATUS_COLORS,
  type BarGeometry,
} from './timeline/range';
import { formatDate } from '../utils/dates';
import ProjectScheduleDrawer from './timeline/ProjectScheduleDrawer.vue';

type ZoomKey = 'month' | 'quarter' | 'year';
const PX_PER_DAY: Record<ZoomKey, number> = { month: 4, quarter: 1.3, year: 0.4 };

const router = useRouter();
const projects = useProjectsStore();
const message = useMessage();

const loading = ref(true);
const items = ref<ProjectSummaryDto[]>([]);
const zoom = ref<ZoomKey>('quarter');
const statusFilter = ref<ProjectStatus | ''>('');
const priorityFilter = ref<ProjectPriority | ''>('');
const includeArchived = ref(false);

const showDrawer = ref(false);
const editTarget = ref<ProjectSummaryDto | null>(null);

const statusOptions = [
  { label: 'All statuses', value: '' },
  ...(['inProgress', 'planned', 'paused', 'idea', 'done', 'abandoned'] as const)
    .map((s) => ({ label: s, value: s })),
];
const priorityOptions = [
  { label: 'All priorities', value: '' },
  { label: 'high',   value: 'high'   },
  { label: 'medium', value: 'medium' },
  { label: 'low',    value: 'low'    },
];
const zoomOptions: Array<{ label: string; value: ZoomKey }> = [
  { label: 'Month',   value: 'month'   },
  { label: 'Quarter', value: 'quarter' },
  { label: 'Year',    value: 'year'    },
];

async function load() {
  loading.value = true;
  try {
    items.value = await projects.fetchAll({ include_archived: includeArchived.value });
  } catch (e) {
    const err = e as { error?: string; detail?: string };
    message.error(`Failed to load: ${err.error ?? 'network error'}${err.detail ? ' — ' + err.detail : ''}`);
  } finally {
    loading.value = false;
  }
}
onMounted(load);

const filtered = computed(() =>
  items.value.filter((p) =>
    (statusFilter.value === '' || p.status === statusFilter.value) &&
    (priorityFilter.value === '' || p.priority === priorityFilter.value)
  ),
);
const sorted = computed(() => sortProjects(filtered.value));

const nowMs = Date.now();
const range = computed(() => computeRange(sorted.value, nowMs));
const pxPerDay = computed(() => PX_PER_DAY[zoom.value]);
const chartWidth = computed(() => Math.max(range.value.totalDays * pxPerDay.value, 600));
const ticks = computed(() => monthTicks(range.value, pxPerDay.value));
const todayLeft = computed(() => ((nowMs - range.value.startMs) / 86_400_000) * pxPerDay.value);

const ROW_HEIGHT = 36;
const LABEL_WIDTH = 220;

interface Row {
  p: ProjectSummaryDto;
  g: BarGeometry | null;
  top: number;
}

const rows = computed<Row[]>(() =>
  sorted.value.map((p, i) => ({
    p,
    g: barGeometry({ project: p, range: range.value, pxPerDay: pxPerDay.value, nowMs }),
    top: i * ROW_HEIGHT,
  })),
);

function openProject(p: ProjectSummaryDto) {
  router.push({ name: 'project-detail', params: { id: String(p.id) } });
}
function editProject(p: ProjectSummaryDto, ev?: Event) {
  ev?.stopPropagation();
  editTarget.value = p;
  showDrawer.value = true;
}
function onKey(p: ProjectSummaryDto, ev: KeyboardEvent) {
  if (ev.key === 'Enter') { openProject(p); ev.preventDefault(); }
  else if (ev.key === ' ' || ev.code === 'Space') { editProject(p); ev.preventDefault(); }
}
</script>

<template>
  <NCard title="Project timeline" :segmented="{ content: 'soft' }">
    <template #header-extra>
      <NSpace align="center">
        <NSelect :options="statusOptions"   v-model:value="statusFilter"   style="width: 160px" />
        <NSelect :options="priorityOptions" v-model:value="priorityFilter" style="width: 160px" />
        <NSelect :options="zoomOptions"     v-model:value="zoom"           style="width: 120px" />
        <NSwitch v-model:value="includeArchived" @update:value="load">
          <template #checked>archived</template>
          <template #unchecked>archived</template>
        </NSwitch>
        <NButton size="small" @click="load">Reload</NButton>
      </NSpace>
    </template>

    <NSpin :show="loading">
      <NEmpty v-if="!loading && rows.length === 0" description="No projects to show." />

      <div v-else class="tl-wrap">
        <!-- Legend -->
        <div class="tl-legend">
          <span v-for="(color, key) in STATUS_COLORS" :key="key" class="tl-legend-item">
            <span class="tl-swatch" :style="{ background: color }" /> {{ key }}
          </span>
          <span class="tl-legend-item"><span class="tl-swatch tl-swatch-ghost" /> unscheduled</span>
          <span class="tl-legend-item"><span class="tl-swatch tl-swatch-open" /> open-ended</span>
        </div>

        <div class="tl-grid" :style="{ '--label-w': `${LABEL_WIDTH}px`, '--row-h': `${ROW_HEIGHT}px` }">
          <!-- Header row -->
          <div class="tl-header-label">Project</div>
          <div class="tl-header-chart">
            <div class="tl-header-inner" :style="{ width: `${chartWidth}px` }">
              <span
                v-for="t in ticks"
                :key="t.leftPx"
                class="tl-tick-label"
                :style="{ left: `${t.leftPx}px` }"
              >{{ t.label }}</span>
            </div>
          </div>

          <!-- Body labels -->
          <div class="tl-body-labels">
            <div
              v-for="row in rows"
              :key="`l-${row.p.id}`"
              class="tl-row-label"
              :class="{ 'tl-archived': row.p.archivedAt }"
              tabindex="0"
              @click="openProject(row.p)"
              @keydown="onKey(row.p, $event)"
            >
              <span class="tl-row-title">{{ row.p.title }}</span>
              <NTag size="tiny" :color="{ color: statusColor(row.p.status), textColor: '#fff', borderColor: statusColor(row.p.status) }">
                {{ row.p.status }}
              </NTag>
            </div>
          </div>

          <!-- Body chart -->
          <div class="tl-body-chart">
            <div class="tl-chart-inner" :style="{ width: `${chartWidth}px`, height: `${rows.length * ROW_HEIGHT}px` }">
              <span
                v-for="t in ticks"
                :key="`g-${t.leftPx}`"
                class="tl-tick-guide"
                :style="{ left: `${t.leftPx}px`, height: `${rows.length * ROW_HEIGHT}px` }"
              />
              <span class="tl-today" :style="{ left: `${todayLeft}px`, height: `${rows.length * ROW_HEIGHT}px` }" />

              <div
                v-for="row in rows"
                :key="`bg-${row.p.id}`"
                class="tl-row-bg"
                :class="{ 'tl-archived': row.p.archivedAt }"
                :style="{ top: `${row.top}px`, height: `${ROW_HEIGHT}px`, width: `${chartWidth}px` }"
              />

              <template v-for="row in rows" :key="`b-${row.p.id}`">
                <NTooltip v-if="row.g" placement="top" :delay="100" :keep-alive-on-hover="false">
                  <template #trigger>
                    <div
                      v-if="row.g.isUnscheduled"
                      class="tl-marker"
                      :class="{ 'tl-archived': row.p.archivedAt }"
                      :style="{
                        top: `${row.top + ROW_HEIGHT / 2 - 7}px`,
                        left: `${row.g.leftPx - 7}px`,
                        borderColor: statusColor(row.p.status),
                      }"
                      @click="openProject(row.p)"
                    />
                    <div
                      v-else
                      class="tl-bar"
                      :class="{
                        'tl-dashed': statusDashed(row.p.status),
                        'tl-inferred': row.g.isInferredStart,
                        'tl-open': row.g.isOpenEnded,
                        'tl-archived': row.p.archivedAt,
                      }"
                      :style="{
                        top: `${row.top + 6}px`,
                        left: `${row.g.leftPx}px`,
                        width: `${row.g.widthPx}px`,
                        height: `${ROW_HEIGHT - 12}px`,
                        background: statusColor(row.p.status),
                        borderColor: statusColor(row.p.status),
                      }"
                      @click="openProject(row.p)"
                    >
                      <button
                        class="tl-bar-edit"
                        :title="`Edit schedule of ${row.p.title}`"
                        @click="(e) => editProject(row.p, e)"
                      >📅</button>
                    </div>
                  </template>
                  <div class="tl-tooltip">
                    <div class="tl-tt-title">{{ row.p.title }}</div>
                    <div>status: <b>{{ row.p.status }}</b> · priority: {{ row.p.priority }} · hw: {{ row.p.hardwareCount }}</div>
                    <div>started: {{ formatDate(row.p.startedAt) }}</div>
                    <div>target: {{ formatDate(row.p.targetDate) }}</div>
                    <div>completed: {{ formatDate(row.p.completedAt) }}</div>
                    <div v-if="row.g.isInferredStart && !row.g.isUnscheduled" class="tl-tt-note">
                      Start inferred from created date; no real <code>startedAt</code> set.
                    </div>
                    <div v-if="row.g.isOpenEnded" class="tl-tt-note">No end date set — bar runs to today.</div>
                    <div v-if="row.g.isUnscheduled" class="tl-tt-note">No schedule set; shown at created date.</div>
                    <div class="tl-tt-hint">Click bar → details · 📅 → edit schedule</div>
                  </div>
                </NTooltip>
              </template>
            </div>
          </div>
        </div>
      </div>
    </NSpin>
  </NCard>

  <ProjectScheduleDrawer
    v-model:show="showDrawer"
    :project="editTarget"
    @saved="load"
  />
</template>

<style scoped>
.tl-wrap { display: flex; flex-direction: column; gap: 12px; }
.tl-legend { display: flex; gap: 14px; flex-wrap: wrap; font-size: 12px; color: var(--n-text-color-3); }
.tl-legend-item { display: inline-flex; align-items: center; gap: 6px; }
.tl-swatch { width: 14px; height: 10px; border-radius: 2px; display: inline-block; }
.tl-swatch-ghost { background: transparent; border: 1.5px solid var(--n-text-color-3); transform: rotate(45deg); width: 10px; height: 10px; }
.tl-swatch-open { background: linear-gradient(to right, #18a058, transparent); }

.tl-grid {
  display: grid;
  grid-template-columns: var(--label-w) 1fr;
  grid-template-rows: 32px auto;
  border: 1px solid var(--n-border-color);
  border-radius: 4px;
  overflow: hidden;
  max-height: calc(100vh - 220px);
}
.tl-header-label, .tl-header-chart {
  background: var(--n-color-modal, rgba(0,0,0,0.04));
  border-bottom: 1px solid var(--n-border-color);
  height: 32px;
  display: flex; align-items: center;
}
.tl-header-label { padding: 0 12px; font-weight: 600; font-size: 12px; }
.tl-header-chart { overflow: hidden; position: relative; }
.tl-header-inner { position: relative; height: 100%; }
.tl-tick-label {
  position: absolute; top: 8px; font-size: 11px; color: var(--n-text-color-3);
  transform: translateX(2px); white-space: nowrap;
}

.tl-body-labels {
  overflow-y: auto; overflow-x: hidden;
  border-right: 1px solid var(--n-border-color);
}
.tl-body-chart { overflow: auto; position: relative; }
.tl-chart-inner { position: relative; }

.tl-row-label {
  height: var(--row-h);
  padding: 0 12px;
  display: flex; align-items: center; justify-content: space-between; gap: 8px;
  border-bottom: 1px solid var(--n-divider-color);
  cursor: pointer;
  outline: none;
}
.tl-row-label:hover { background: var(--n-action-color, rgba(0,0,0,0.04)); }
.tl-row-label:focus-visible { box-shadow: inset 0 0 0 2px var(--n-primary-color, #18a058); }
.tl-row-title { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 13px; }

.tl-row-bg { position: absolute; left: 0; border-bottom: 1px solid var(--n-divider-color); }
.tl-tick-guide { position: absolute; top: 0; width: 1px; background: var(--n-divider-color); }
.tl-today { position: absolute; top: 0; width: 2px; background: #d03050; opacity: 0.8; pointer-events: none; }

.tl-bar {
  position: absolute;
  border-radius: 3px;
  border: 1px solid;
  cursor: pointer;
  display: flex; align-items: center; justify-content: flex-end;
  padding-right: 2px;
  box-shadow: 0 1px 2px rgba(0,0,0,0.15);
  transition: filter 120ms;
  min-width: 6px;
}
.tl-bar:hover { filter: brightness(1.08); }
.tl-bar.tl-dashed { border-style: dashed; opacity: 0.75; }
.tl-bar.tl-inferred {
  background-image: repeating-linear-gradient(
    45deg, rgba(255,255,255,0.18) 0 4px, transparent 4px 8px
  );
}
.tl-bar.tl-open {
  -webkit-mask-image: linear-gradient(to right, #000 70%, transparent 100%);
          mask-image: linear-gradient(to right, #000 70%, transparent 100%);
}
.tl-bar.tl-archived, .tl-marker.tl-archived, .tl-row-label.tl-archived { opacity: 0.4; }
.tl-bar-edit {
  background: transparent; border: none; color: #fff; font-size: 11px;
  cursor: pointer; padding: 0 2px; opacity: 0; transition: opacity 80ms;
}
.tl-bar:hover .tl-bar-edit { opacity: 1; }
.tl-bar-edit:focus-visible { opacity: 1; outline: 2px solid #fff; }

.tl-marker {
  position: absolute;
  width: 14px; height: 14px;
  border: 2px solid;
  background: var(--n-color, #fff);
  transform: rotate(45deg);
  cursor: pointer;
}

.tl-tooltip { font-size: 12px; line-height: 1.4; max-width: 320px; }
.tl-tt-title { font-weight: 600; margin-bottom: 4px; }
.tl-tt-note  { margin-top: 4px; color: #f0a020; }
.tl-tt-hint  { margin-top: 6px; color: var(--n-text-color-3); font-size: 11px; }

@media (prefers-reduced-motion: reduce) {
  .tl-bar { transition: none; }
}
</style>
