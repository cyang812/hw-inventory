<script setup lang="ts">
import { onMounted } from 'vue';
import { useRoute, RouterLink } from 'vue-router';
import { NCard, NSpace, NTag, NDescriptions, NDescriptionsItem, NList, NListItem, NSelect, NButton, useMessage } from 'naive-ui';
import { useProjectsStore } from '../stores/projects';

const route = useRoute();
const projects = useProjectsStore();
const message = useMessage();
const id = Number(route.params.id);

const statusOptions = ['idea', 'planned', 'inProgress', 'paused', 'done', 'abandoned'].map((s) => ({ label: s, value: s }));

onMounted(async () => {
  try {
    await projects.fetchDetail(id);
  } catch (e) {
    const err = e as { status?: number; error?: string; detail?: string };
    message.error(`Failed to load: ${err.error ?? 'network error'}${err.detail ? ' — ' + err.detail : ''}`);
  }
});

async function changeStatus(newStatus: string) {
  await projects.patch(id, [{ op: 'replace', path: '/status', value: newStatus }]);
  message.success(`Status → ${newStatus}`);
}
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
        <NSelect :options="statusOptions" :value="projects.detail.status" @update:value="changeStatus" style="width: 180px" />
      </NSpace>
      <p style="margin-top: 12px; white-space: pre-wrap;">{{ projects.detail.description || 'No description.' }}</p>
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

    <RouterLink to="/projects"><NButton>Back to project list</NButton></RouterLink>
  </NSpace>
</template>
