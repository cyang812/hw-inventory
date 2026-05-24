<script setup lang="ts">
import { onMounted, ref, computed, h } from 'vue';
import { RouterLink } from 'vue-router';
import {
  NCard, NDataTable, NSpace, NTag, NButton, NDrawer, NDrawerContent,
  NForm, NFormItem, NInput, NSelect, NSwitch, useMessage,
} from 'naive-ui';
import type { DataTableColumns } from 'naive-ui';
import { useProjectsStore } from '../stores/projects';
import type { ProjectSummaryDto } from '../types/api';

const projects = useProjectsStore();
const message = useMessage();

const showDrawer = ref(false);
const showArchived = ref(false);
const draft = ref({ title: '', description: '', status: 'idea', priority: 'medium' });

const statusOptions = ['idea', 'planned', 'inProgress', 'paused', 'done', 'abandoned'].map((s) => ({ label: s, value: s }));
const priorityOptions = ['low', 'medium', 'high'].map((s) => ({ label: s, value: s }));

onMounted(async () => {
  try {
    await projects.fetchList({ include_archived: showArchived.value });
  } catch (e) {
    const err = e as { status?: number; error?: string; detail?: string };
    message.error(`Failed to load: ${err.error ?? 'network error'}${err.detail ? ' — ' + err.detail : ''}`);
  }
});

async function reload() {
  await projects.fetchList({ include_archived: showArchived.value });
}

async function save() {
  if (!draft.value.title.trim()) { message.error('Title is required'); return; }
  await projects.create(draft.value);
  showDrawer.value = false;
  draft.value = { title: '', description: '', status: 'idea', priority: 'medium' };
  await reload();
  message.success('Project created');
}
async function archive(id: number) {
  await projects.archive(id);
  message.success('Archived');
}

const columns = computed<DataTableColumns<ProjectSummaryDto>>(() => [
  { title: 'Title', key: 'title', render: (row) => h(RouterLink, { to: `/projects/${row.id}` }, () => row.title) },
  { title: 'Status', key: 'status', render: (row) => h(NTag, { size: 'small' }, () => row.status) },
  { title: 'Priority', key: 'priority' },
  { title: 'HW', key: 'hardwareCount' },
  { title: 'Target', key: 'targetDate', render: (row) => row.targetDate ? new Date(row.targetDate).toLocaleDateString() : '—' },
  {
    title: 'Actions', key: 'actions',
    render: (row) => row.archivedAt ? h(NTag, { type: 'warning' }, () => 'archived')
      : h(NButton, { size: 'tiny', type: 'error', onClick: () => archive(row.id) }, () => 'Archive'),
  },
]);
</script>

<template>
  <NCard title="Projects" :segmented="{ content: 'soft' }">
    <template #header-extra>
      <NSpace>
        <NSwitch v-model:value="showArchived" @update:value="reload">
          <template #checked>show archived</template>
          <template #unchecked>show archived</template>
        </NSwitch>
        <NButton type="primary" @click="showDrawer = true">+ New project</NButton>
      </NSpace>
    </template>
    <NDataTable :columns="columns" :data="projects.list.items" :loading="projects.loading" :bordered="false" :row-key="(row: ProjectSummaryDto) => row.id" />
  </NCard>

  <NDrawer v-model:show="showDrawer" :width="420">
    <NDrawerContent title="New project" closable>
      <NForm label-placement="top">
        <NFormItem label="Title" required><NInput v-model:value="draft.title" /></NFormItem>
        <NFormItem label="Description"><NInput type="textarea" v-model:value="draft.description" /></NFormItem>
        <NFormItem label="Status"><NSelect :options="statusOptions" v-model:value="draft.status" /></NFormItem>
        <NFormItem label="Priority"><NSelect :options="priorityOptions" v-model:value="draft.priority" /></NFormItem>
        <NButton type="primary" block @click="save">Create</NButton>
      </NForm>
    </NDrawerContent>
  </NDrawer>
</template>
