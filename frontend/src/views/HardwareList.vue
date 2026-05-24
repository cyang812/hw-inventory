<script setup lang="ts">
import { onMounted, ref, reactive, computed, watch, h } from 'vue';
import { RouterLink } from 'vue-router';
import {
  NCard, NDataTable, NSpace, NInput, NSelect, NSwitch, NButton, NTag, NDrawer,
  NDrawerContent, NForm, NFormItem, NInputNumber, useMessage,
} from 'naive-ui';
import type { DataTableColumns } from 'naive-ui';
import { useHardwareStore } from '../stores/hardware';
import { useSettingsStore } from '../stores/settings';
import type { HardwareSummaryDto } from '../types/api';

const hwStore = useHardwareStore();
const settings = useSettingsStore();
const message = useMessage();

const statusOptions = ['available', 'inUse', 'loaned', 'archived', 'sold', 'lost'].map((s) => ({ label: s, value: s }));
const conditionOptions = ['working', 'partial', 'broken', 'unknown'].map((s) => ({ label: s, value: s }));

const filter = reactive({
  q: '',
  category: [] as number[],
  tag: [] as number[],
  status: null as string | null,
  condition: null as string | null,
  idle_days: null as number | null,
  include_archived: false,
});

const showDrawer = ref(false);
const draft = reactive({ name: '', manufacturer: '', model: '', status: 'available', condition: 'working', location: '', notes: '' });

const categoryOptions = computed(() => settings.categories.map((c) => ({ label: c.name, value: c.id })));
const tagOptions = computed(() => settings.tags.map((t) => ({ label: t.name, value: t.id })));

async function refresh() {
  await hwStore.fetchList({
    q: filter.q || undefined,
    category: filter.category.length ? filter.category : undefined,
    tag: filter.tag.length ? filter.tag : undefined,
    status: filter.status || undefined,
    condition: filter.condition || undefined,
    idle_days: filter.idle_days || undefined,
    include_archived: filter.include_archived,
    offset: 0,
  });
}

onMounted(async () => {
  try {
    await Promise.all([settings.fetchCategories(), settings.fetchTags(), refresh()]);
  } catch (e) {
    const err = e as { status?: number; error?: string; detail?: string };
    message.error(`Failed to load: ${err.error ?? 'network error'}${err.detail ? ' — ' + err.detail : ''}`);
  }
});

watch([
  () => filter.category,
  () => filter.tag,
  () => filter.status,
  () => filter.condition,
  () => filter.idle_days,
  () => filter.include_archived,
], refresh, { deep: true });

let qTimer: number | undefined;
watch(() => filter.q, () => {
  window.clearTimeout(qTimer);
  qTimer = window.setTimeout(refresh, 250);
});

async function markUsed(id: number) {
  await hwStore.markUsedToday(id);
  message.success('Marked as used today');
}
async function archive(id: number) {
  await hwStore.archive(id);
  message.success('Archived');
}
async function unarchive(id: number) {
  await hwStore.unarchive(id);
  message.success('Unarchived');
}

async function saveDraft() {
  if (!draft.name.trim()) { message.error('Name is required'); return; }
  try {
    await hwStore.create({ ...draft });
    showDrawer.value = false;
    Object.assign(draft, { name: '', manufacturer: '', model: '', status: 'available', condition: 'working', location: '', notes: '' });
    await refresh();
    message.success('Hardware created');
  } catch (e) {
    message.error('Create failed');
  }
}

const columns = computed<DataTableColumns<HardwareSummaryDto>>(() => [
  {
    title: 'Name', key: 'name',
    render: (row) => h(RouterLink, { to: `/hardware/${row.id}` }, () => row.name),
  },
  { title: 'Mfr / Model', key: 'mfr', render: (row) => [row.manufacturer, row.model].filter(Boolean).join(' / ') },
  { title: 'Status', key: 'status', render: (row) => h(NTag, { size: 'small', type: row.status === 'archived' ? 'warning' : 'default' }, () => row.status) },
  { title: 'Condition', key: 'condition', render: (row) => h(NTag, { size: 'small' }, () => row.condition) },
  { title: 'Location', key: 'location' },
  { title: 'Last used', key: 'lastUsedAt', render: (row) => row.lastUsedAt ? new Date(row.lastUsedAt).toLocaleDateString() : '—' },
  {
    title: 'Categories', key: 'categories',
    render: (row) => h(NSpace, { size: 'small' }, () => row.categories.map((c) => h(NTag, { size: 'small', type: 'info' }, () => c))),
  },
  {
    title: 'Actions', key: 'actions',
    render: (row) => h(NSpace, { size: 'small' }, () => [
      h(NButton, { size: 'tiny', onClick: () => markUsed(row.id) }, () => 'Use today'),
      row.archivedAt
        ? h(NButton, { size: 'tiny', type: 'warning', onClick: () => unarchive(row.id) }, () => 'Unarchive')
        : h(NButton, { size: 'tiny', type: 'error', onClick: () => archive(row.id) }, () => 'Archive'),
    ]),
  },
]);
</script>

<template>
  <NCard title="Hardware" :segmented="{ content: 'soft' }">
    <template #header-extra>
      <NButton type="primary" @click="showDrawer = true">+ Add hardware</NButton>
    </template>

    <NSpace vertical size="small">
      <NSpace>
        <NInput v-model:value="filter.q" placeholder="Search name, model, notes…" style="width: 260px" clearable />
        <NSelect multiple :options="categoryOptions" v-model:value="filter.category" placeholder="Category" style="min-width: 200px" clearable />
        <NSelect multiple :options="tagOptions" v-model:value="filter.tag" placeholder="Tag" style="min-width: 160px" clearable />
        <NSelect :options="statusOptions" v-model:value="filter.status" placeholder="Status" style="width: 140px" clearable />
        <NSelect :options="conditionOptions" v-model:value="filter.condition" placeholder="Condition" style="width: 140px" clearable />
        <NInputNumber v-model:value="filter.idle_days" placeholder="Idle ≥ days" style="width: 140px" />
        <NSwitch v-model:value="filter.include_archived">
          <template #checked>show archived</template>
          <template #unchecked>show archived</template>
        </NSwitch>
      </NSpace>

      <NDataTable :columns="columns" :data="hwStore.list.items" :loading="hwStore.loading" :bordered="false" :row-key="(row: HardwareSummaryDto) => row.id" />
    </NSpace>
  </NCard>

  <NDrawer v-model:show="showDrawer" :width="420">
    <NDrawerContent title="New hardware" closable>
      <NForm label-placement="top">
        <NFormItem label="Name" required>
          <NInput v-model:value="draft.name" placeholder="e.g. Raspberry Pi 4B" />
        </NFormItem>
        <NFormItem label="Manufacturer"><NInput v-model:value="draft.manufacturer" /></NFormItem>
        <NFormItem label="Model"><NInput v-model:value="draft.model" /></NFormItem>
        <NFormItem label="Status">
          <NSelect :options="statusOptions" v-model:value="draft.status" />
        </NFormItem>
        <NFormItem label="Condition">
          <NSelect :options="conditionOptions" v-model:value="draft.condition" />
        </NFormItem>
        <NFormItem label="Location"><NInput v-model:value="draft.location" placeholder="shelf / drawer / room" /></NFormItem>
        <NFormItem label="Notes"><NInput type="textarea" v-model:value="draft.notes" /></NFormItem>
        <NButton type="primary" block @click="saveDraft">Create</NButton>
      </NForm>
    </NDrawerContent>
  </NDrawer>
</template>
