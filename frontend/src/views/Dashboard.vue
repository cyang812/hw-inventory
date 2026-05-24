<script setup lang="ts">
import { onMounted, computed } from 'vue';
import { useDashboardStore } from '../stores/dashboard';
import { useProjectsStore } from '../stores/projects';
import { NCard, NGrid, NGi, NStatistic, NTag, NList, NListItem, NSpace, NThing, NEmpty, NButton, useMessage } from 'naive-ui';
import { RouterLink } from 'vue-router';

const dash = useDashboardStore();
const projects = useProjectsStore();
const message = useMessage();

onMounted(async () => {
  try {
    await Promise.all([dash.refresh(), projects.fetchList({ include_archived: false })]);
  } catch (e) {
    const err = e as { status?: number; error?: string; detail?: string };
    message.error(`Failed to load dashboard: ${err.error ?? 'network error'}${err.detail ? ' — ' + err.detail : ''}`);
  }
});

async function reload() {
  try {
    await Promise.all([dash.refresh(), projects.fetchList({ include_archived: false })]);
    message.success('Refreshed');
  } catch (e) {
    const err = e as { status?: number; error?: string; detail?: string };
    message.error(`Refresh failed: ${err.error ?? 'network error'}`);
  }
}

const lanes: Array<{ status: string; label: string }> = [
  { status: 'idea', label: 'Idea' },
  { status: 'planned', label: 'Planned' },
  { status: 'inProgress', label: 'In progress' },
  { status: 'paused', label: 'Paused' },
  { status: 'done', label: 'Done' },
];

const byLane = computed(() => {
  const map: Record<string, typeof projects.list.items> = {};
  for (const lane of lanes) map[lane.status] = [];
  for (const p of projects.list.items) {
    if (!map[p.status]) map[p.status] = [];
    map[p.status].push(p);
  }
  return map;
});

function relative(when?: string | null): string {
  if (!when) return 'never';
  const diff = (Date.now() - new Date(when).getTime()) / 86_400_000;
  if (diff < 1) return 'today';
  if (diff < 30) return `${Math.round(diff)}d ago`;
  if (diff < 365) return `${Math.round(diff / 30)}mo ago`;
  return `${Math.round(diff / 365)}y ago`;
}
</script>

<template>
  <NSpace vertical size="large">
    <NSpace justify="end">
      <NButton size="small" @click="reload">Reload</NButton>
    </NSpace>

    <NGrid :cols="4" :x-gap="16" :y-gap="16" responsive="screen">
      <NGi><NCard><NStatistic label="Hardware" :value="dash.stats?.hardwareTotal ?? 0" /></NCard></NGi>
      <NGi><NCard><NStatistic label="Projects" :value="dash.stats?.projectsTotal ?? 0" /></NCard></NGi>
      <NGi><NCard><NStatistic label="Idle items" :value="dash.stats?.idleCount ?? 0" /></NCard></NGi>
      <NGi><NCard><NStatistic label="Overdue loans" :value="dash.stats?.overdueLoans ?? 0" /></NCard></NGi>
    </NGrid>

    <NGrid :cols="2" :x-gap="16" :y-gap="16" responsive="screen">
      <NGi>
        <NCard title="Idle hardware" :segmented="{ content: 'soft' }">
          <NEmpty v-if="dash.idle.length === 0" description="Nothing has gone idle yet — great!" />
          <NList v-else>
            <NListItem v-for="h in dash.idle" :key="h.id">
              <NThing :title="h.name">
                <template #description>
                  <NSpace size="small">
                    <NTag size="small" type="info">{{ h.condition }}</NTag>
                    <NTag size="small">{{ h.status }}</NTag>
                    <span>last used {{ relative(h.lastUsedAt ?? h.createdAt) }}</span>
                  </NSpace>
                </template>
                <RouterLink :to="`/hardware/${h.id}`">Open</RouterLink>
              </NThing>
            </NListItem>
          </NList>
        </NCard>
      </NGi>
      <NGi>
        <NCard title="Got nothing for it? (Suggestions)" :segmented="{ content: 'soft' }">
          <NEmpty v-if="dash.suggestions.length === 0" description="Every idle item has a planned project." />
          <NList v-else>
            <NListItem v-for="h in dash.suggestions" :key="h.id">
              <NThing :title="h.name">
                <template #description>
                  <NSpace size="small">
                    <NTag v-for="c in h.categories" :key="c" size="small">{{ c }}</NTag>
                  </NSpace>
                </template>
                <RouterLink :to="`/hardware/${h.id}`">Open</RouterLink>
              </NThing>
            </NListItem>
          </NList>
        </NCard>
      </NGi>
    </NGrid>

    <NCard title="Project board" :segmented="{ content: 'soft' }">
      <NGrid :cols="lanes.length" :x-gap="12" responsive="screen">
        <NGi v-for="lane in lanes" :key="lane.status">
          <h4 style="margin-top: 0">{{ lane.label }} ({{ byLane[lane.status]?.length ?? 0 }})</h4>
          <NEmpty v-if="!byLane[lane.status]?.length" description="—" size="small" />
          <NSpace vertical size="small">
            <NCard v-for="p in byLane[lane.status]" :key="p.id" size="small" hoverable>
              <RouterLink :to="`/projects/${p.id}`" style="font-weight: 500">{{ p.title }}</RouterLink>
              <div style="font-size: 12px; color: var(--n-text-color-3);">{{ p.priority }} · {{ p.hardwareCount }} hw</div>
            </NCard>
          </NSpace>
        </NGi>
      </NGrid>
    </NCard>

    <NCard v-if="dash.overdue.length" title="Overdue loans">
      <NList>
        <NListItem v-for="l in dash.overdue" :key="l.id">
          <NSpace>
            <span>Loan #{{ l.id }} to <strong>{{ l.loanedTo }}</strong></span>
            <NTag type="error">due {{ relative(l.dueAt) }}</NTag>
            <RouterLink :to="`/hardware/${l.hardwareId}`">Open hardware</RouterLink>
          </NSpace>
        </NListItem>
      </NList>
    </NCard>
  </NSpace>
</template>
