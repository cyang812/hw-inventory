<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { NCard, NSpace, NTag, NInput, NButton, NList, NListItem, NThing, useMessage } from 'naive-ui';
import { useSettingsStore } from '../stores/settings';
import { api } from '../api/client';

const settings = useSettingsStore();
const message = useMessage();

const newCat = ref({ name: '', slug: '', description: '' });
const newTag = ref({ name: '', color: '' });

onMounted(async () => {
  try {
    await Promise.all([settings.fetchCategories(), settings.fetchTags()]);
  } catch (e) {
    const err = e as { status?: number; error?: string; detail?: string };
    message.error(`Failed to load: ${err.error ?? 'network error'}${err.detail ? ' — ' + err.detail : ''}`);
  }
});

async function addCat() {
  if (!newCat.value.name) return;
  await settings.createCategory(newCat.value);
  newCat.value = { name: '', slug: '', description: '' };
  message.success('Category added');
}
async function addTag() {
  if (!newTag.value.name) return;
  await settings.createTag(newTag.value);
  newTag.value = { name: '', color: '' };
  message.success('Tag added');
}
async function delCat(id: number) {
  await settings.deleteCategory(id);
  message.success('Removed');
}
async function delTag(id: number) {
  await settings.deleteTag(id);
  message.success('Removed');
}

async function exportJson() {
  const text = await api.get<string>('/api/export/json');
  const blob = new Blob([typeof text === 'string' ? text : JSON.stringify(text)], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `hw-inventory-${new Date().toISOString().slice(0, 10)}.json`;
  a.click();
  URL.revokeObjectURL(url);
}

const importFile = ref<File | null>(null);
async function importJson() {
  if (!importFile.value) return;
  const body = await importFile.value.text();
  const resp = await fetch('/api/import/json', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body });
  if (resp.ok) message.success('Imported');
  else message.error('Import failed');
}
</script>

<template>
  <NSpace vertical size="large">
    <NCard title="Categories">
      <NSpace style="margin-bottom: 12px">
        <NInput v-model:value="newCat.name" placeholder="Name" />
        <NInput v-model:value="newCat.slug" placeholder="Slug (optional)" />
        <NInput v-model:value="newCat.description" placeholder="Description" />
        <NButton type="primary" @click="addCat">Add</NButton>
      </NSpace>
      <NList>
        <NListItem v-for="c in settings.categories" :key="c.id">
          <NThing :title="c.name" :description="c.description ?? ''">
            <NTag size="small">{{ c.slug }}</NTag>
          </NThing>
          <template #suffix>
            <NButton size="tiny" type="error" @click="delCat(c.id)">Delete</NButton>
          </template>
        </NListItem>
      </NList>
    </NCard>

    <NCard title="Tags">
      <NSpace style="margin-bottom: 12px">
        <NInput v-model:value="newTag.name" placeholder="Name" />
        <NInput v-model:value="newTag.color" placeholder="Color (#abcdef)" />
        <NButton type="primary" @click="addTag">Add</NButton>
      </NSpace>
      <NList>
        <NListItem v-for="t in settings.tags" :key="t.id">
          <NSpace align="center">
            <NTag :color="t.color ? { color: t.color, textColor: '#fff' } : undefined">{{ t.name }}</NTag>
          </NSpace>
          <template #suffix>
            <NButton size="tiny" type="error" @click="delTag(t.id)">Delete</NButton>
          </template>
        </NListItem>
      </NList>
    </NCard>

    <NCard title="Import / Export">
      <NSpace>
        <NButton @click="exportJson">Export JSON</NButton>
        <input type="file" accept="application/json" @change="(e) => importFile = (e.target as HTMLInputElement).files?.[0] ?? null" />
        <NButton type="primary" @click="importJson" :disabled="!importFile">Import JSON</NButton>
      </NSpace>
    </NCard>
  </NSpace>
</template>
