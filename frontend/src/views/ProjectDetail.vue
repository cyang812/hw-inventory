<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useRoute, RouterLink } from 'vue-router';
import {
  NCard, NSpace, NTag, NDescriptions, NDescriptionsItem, NList, NListItem,
  NButton, useMessage,
} from 'naive-ui';
import { useProjectsStore } from '../stores/projects';
import { formatDate, diffDays } from '../utils/dates';
import ProjectScheduleDrawer from './timeline/ProjectScheduleDrawer.vue';

const route = useRoute();
const projects = useProjectsStore();
const message = useMessage();
const id = Number(route.params.id);

const showDrawer = ref(false);

onMounted(async () => {
  try {
    await projects.fetchDetail(id);
  } catch (e) {
    const err = e as { status?: number; error?: string; detail?: string };
    message.error(`Failed to load: ${err.error ?? 'network error'}${err.detail ? ' — ' + err.detail : ''}`);
  }
});

const duration = computed(() => {
  const d = projects.detail;
  if (!d?.startedAt) return null;
  const end = d.completedAt ?? new Date().toISOString();
  const days = diffDays(d.startedAt, end);
  if (days == null) return null;
  const suffix = d.completedAt ? '' : ' (so far)';
  return `${days} day${days === 1 ? '' : 's'}${suffix}`;
});
</script>

<template>
  <div v-if="!projects.detail">Loading…</div>
  <NSpace v-else vertical size="large">
    <NCard>
      <NSpace align="center">
        <h2 style="margin: 0">{{ projects.detail.title }}</h2>
        <NTag v-if="projects.detail.archivedAt" type="warning">archived</NTag>
        <NTag>{{ projects.detail.status }}</NTag>
        <NTag type="info">{{ projects.detail.priority }}</NTag>
        <NButton size="small" @click="showDrawer = true">Edit schedule…</NButton>
      </NSpace>
      <p style="margin-top: 12px; white-space: pre-wrap;">{{ projects.detail.description || 'No description.' }}</p>
    </NCard>

    <NCard title="Schedule" :segmented="{ content: 'soft' }">
      <NDescriptions :column="2" bordered>
        <NDescriptionsItem label="Started">{{ formatDate(projects.detail.startedAt) }}</NDescriptionsItem>
        <NDescriptionsItem label="Target">{{ formatDate(projects.detail.targetDate) }}</NDescriptionsItem>
        <NDescriptionsItem label="Completed">{{ formatDate(projects.detail.completedAt) }}</NDescriptionsItem>
        <NDescriptionsItem label="Duration">{{ duration ?? '—' }}</NDescriptionsItem>
        <NDescriptionsItem label="Created">{{ formatDate(projects.detail.createdAt) }}</NDescriptionsItem>
        <NDescriptionsItem label="Last update">{{ formatDate(projects.detail.updatedAt) }}</NDescriptionsItem>
      </NDescriptions>
    </NCard>

    <NCard title="Linked hardware">
      <NList v-if="projects.detail.hardware.length">
        <NListItem v-for="h in projects.detail.hardware" :key="h.hardwareId">
          <NSpace align="center">
            <RouterLink :to="`/hardware/${h.hardwareId}`"><strong>{{ h.hardwareName }}</strong></RouterLink>
            <NTag v-if="h.role" size="small">{{ h.role }}</NTag>
          </NSpace>
        </NListItem>
      </NList>
      <NDescriptions v-else><NDescriptionsItem>No hardware linked yet</NDescriptionsItem></NDescriptions>
    </NCard>

    <NSpace>
      <RouterLink to="/projects"><NButton>Back to project list</NButton></RouterLink>
      <RouterLink to="/projects/timeline"><NButton>Open timeline</NButton></RouterLink>
    </NSpace>
  </NSpace>

  <ProjectScheduleDrawer
    v-model:show="showDrawer"
    :project="projects.detail"
    @saved="() => projects.fetchDetail(id)"
  />
</template>
