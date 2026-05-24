<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import {
  NDrawer, NDrawerContent, NForm, NFormItem, NDatePicker, NSelect, NSpace, NButton,
  NAlert, useMessage,
} from 'naive-ui';
import { useProjectsStore } from '../../stores/projects';
import type { ProjectSummaryDto, ProjectDto, ProjectStatus } from '../../types/api';
import { toIsoNoonUtc, parseIsoToLocalMidnight } from '../../utils/dates';

// Shared edit drawer for status + the three planning dates. Used by both the
// Gantt timeline and the project detail page so editing logic stays in one place.
const props = defineProps<{ show: boolean; project: ProjectSummaryDto | ProjectDto | null }>();
const emit = defineEmits<{ (e: 'update:show', v: boolean): void; (e: 'saved'): void }>();

const projects = useProjectsStore();
const message = useMessage();

const statusOptions = ['idea', 'planned', 'inProgress', 'paused', 'done', 'abandoned']
  .map((s) => ({ label: s, value: s }));

const status     = ref<ProjectStatus>('idea');
const startedMs  = ref<number | null>(null);
const targetMs   = ref<number | null>(null);
const completeMs = ref<number | null>(null);
const saving     = ref(false);

watch(
  () => props.project,
  (p) => {
    if (!p) return;
    status.value     = p.status;
    startedMs.value  = parseIsoToLocalMidnight(p.startedAt);
    targetMs.value   = parseIsoToLocalMidnight(p.targetDate);
    completeMs.value = parseIsoToLocalMidnight(p.completedAt);
  },
  { immediate: true },
);

const warning = computed(() => {
  if (startedMs.value && targetMs.value && targetMs.value < startedMs.value)
    return 'Target date is before start date.';
  if (startedMs.value && completeMs.value && completeMs.value < startedMs.value)
    return 'Completion date is before start date.';
  if (status.value === 'done' && !completeMs.value)
    return 'Status is "done" but completion date is empty — it will be set to today on save.';
  return null;
});

async function save() {
  if (!props.project) return;
  saving.value = true;
  try {
    const ops: Array<{ op: string; path: string; value: string | null }> = [];
    if (status.value !== props.project.status) {
      ops.push({ op: 'replace', path: '/status', value: status.value });
    }
    const newStarted  = toIsoNoonUtc(startedMs.value);
    const newTarget   = toIsoNoonUtc(targetMs.value);
    let   newComplete = toIsoNoonUtc(completeMs.value);
    if (status.value === 'done' && !newComplete) newComplete = toIsoNoonUtc(Date.now());

    pushIfChanged(ops, '/startedAt',   props.project.startedAt,   newStarted);
    pushIfChanged(ops, '/targetDate',  props.project.targetDate,  newTarget);
    pushIfChanged(ops, '/completedAt', props.project.completedAt, newComplete);

    if (ops.length === 0) {
      message.info('No changes.');
      emit('update:show', false);
      return;
    }
    await projects.patch(props.project.id, ops);
    message.success('Schedule saved');
    emit('saved');
    emit('update:show', false);
  } catch (e) {
    const err = e as { error?: string; detail?: string };
    message.error(`Save failed: ${err.error ?? 'network error'}${err.detail ? ' — ' + err.detail : ''}`);
  } finally {
    saving.value = false;
  }
}

function pushIfChanged(
  ops: Array<{ op: string; path: string; value: string | null }>,
  path: string,
  oldIso: string | null | undefined,
  newIso: string | null,
) {
  // Compare on calendar-day only.
  const oldDay = oldIso ? toIsoNoonUtc(new Date(oldIso)) : null;
  if (oldDay === newIso) return;
  ops.push({ op: 'replace', path, value: newIso });
}

function markStartedNow() { startedMs.value = todayMidnight(); }
function markDoneNow()    { completeMs.value = todayMidnight(); status.value = 'done'; }
function clearStarted()   { startedMs.value = null; }
function clearTarget()    { targetMs.value = null; }
function clearComplete()  { completeMs.value = null; }

function todayMidnight(): number {
  const d = new Date();
  return new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
}
</script>

<template>
  <NDrawer :show="show" :width="420" @update:show="(v) => emit('update:show', v)">
    <NDrawerContent :title="project ? `Edit schedule — ${project.title}` : 'Edit schedule'" closable>
      <NForm v-if="project" label-placement="top">
        <NFormItem label="Status">
          <NSelect :options="statusOptions" v-model:value="status" />
        </NFormItem>
        <NFormItem label="Started">
          <NSpace style="width: 100%">
            <NDatePicker v-model:value="startedMs" type="date" clearable style="flex: 1" />
            <NButton size="small" @click="markStartedNow">Today</NButton>
            <NButton size="small" quaternary @click="clearStarted">Clear</NButton>
          </NSpace>
        </NFormItem>
        <NFormItem label="Target">
          <NSpace style="width: 100%">
            <NDatePicker v-model:value="targetMs" type="date" clearable style="flex: 1" />
            <NButton size="small" quaternary @click="clearTarget">Clear</NButton>
          </NSpace>
        </NFormItem>
        <NFormItem label="Completed">
          <NSpace style="width: 100%">
            <NDatePicker v-model:value="completeMs" type="date" clearable style="flex: 1" />
            <NButton size="small" @click="markDoneNow">Done now</NButton>
            <NButton size="small" quaternary @click="clearComplete">Clear</NButton>
          </NSpace>
        </NFormItem>
        <NAlert v-if="warning" type="warning" :show-icon="true" style="margin-bottom: 12px;">
          {{ warning }}
        </NAlert>
        <NSpace>
          <NButton type="primary" :loading="saving" @click="save">Save</NButton>
          <NButton @click="emit('update:show', false)">Cancel</NButton>
        </NSpace>
      </NForm>
    </NDrawerContent>
  </NDrawer>
</template>
