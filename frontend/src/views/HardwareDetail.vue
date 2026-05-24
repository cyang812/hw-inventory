<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { useRoute, RouterLink } from 'vue-router';
import {
  NCard, NSpace, NTag, NTabs, NTabPane, NTimeline, NTimelineItem,
  NButton, NDescriptions, NDescriptionsItem, NEmpty, NInput, NSelect,
  NForm, NFormItem, NDatePicker, useMessage,
} from 'naive-ui';
import { useHardwareStore } from '../stores/hardware';
import { useProjectsStore } from '../stores/projects';

const route = useRoute();
const hwStore = useHardwareStore();
const projects = useProjectsStore();
const message = useMessage();

const id = Number(route.params.id);
const activityKind = ref('used');
const activityDesc = ref('');
const activityWhen = ref<number | null>(null);
const linkProjectId = ref<number | null>(null);
const linkRole = ref('');

const configKind = ref('firmware');
const configName = ref('');
const configVersion = ref('');

const loanTo = ref('');
const loanDue = ref<number | null>(null);

onMounted(async () => {
  try {
    await Promise.all([hwStore.fetchDetail(id), projects.fetchList()]);
  } catch (e) {
    const err = e as { status?: number; error?: string; detail?: string };
    message.error(`Failed to load: ${err.error ?? 'network error'}${err.detail ? ' — ' + err.detail : ''}`);
  }
});

const kinds = ['used', 'flashed', 'repaired', 'measured', 'configured', 'inspected', 'moved', 'note']
  .map((k) => ({ label: k, value: k }));
const configKinds = ['firmware', 'os', 'bootloader', 'config'].map((k) => ({ label: k, value: k }));

async function logActivity() {
  await hwStore.logActivity(id, {
    kind: activityKind.value,
    description: activityDesc.value || undefined,
    occurredAt: activityWhen.value ? new Date(activityWhen.value).toISOString() : undefined,
  });
  activityDesc.value = '';
  activityWhen.value = null;
  message.success('Activity logged');
}
async function markUsed() {
  await hwStore.markUsedToday(id);
  message.success('Marked as used today');
}
async function linkProject() {
  if (!linkProjectId.value) return;
  await hwStore.linkProject(id, linkProjectId.value, linkRole.value || undefined);
  linkProjectId.value = null; linkRole.value = '';
  message.success('Linked');
}
async function unlink(pid: number) {
  await hwStore.unlinkProject(id, pid);
}
async function recordConfig() {
  if (!configName.value) { message.error('name required'); return; }
  await hwStore.recordConfig(id, {
    kind: configKind.value,
    name: configName.value,
    version: configVersion.value || undefined,
    isCurrent: true,
  });
  configName.value = ''; configVersion.value = '';
  message.success('Recorded');
}
async function startLoan() {
  if (!loanTo.value) { message.error('Loaned to required'); return; }
  await hwStore.startLoan(id, {
    loanedTo: loanTo.value,
    dueAt: loanDue.value ? new Date(loanDue.value).toISOString() : undefined,
  });
  loanTo.value = ''; loanDue.value = null;
  message.success('Loan opened');
}
async function returnLoan(loanId: number) {
  await hwStore.returnLoan(id, loanId);
  message.success('Loan closed');
}

const projectOptions = (() => projects.list.items.map((p) => ({ label: p.title, value: p.id })));
</script>

<template>
  <div v-if="!hwStore.detail">Loading…</div>
  <NSpace v-else vertical size="large">
    <NCard>
      <NSpace align="center">
        <h2 style="margin: 0">{{ hwStore.detail.name }}</h2>
        <NTag v-if="hwStore.detail.archivedAt" type="warning">archived</NTag>
        <NTag>{{ hwStore.detail.status }}</NTag>
        <NTag type="info">{{ hwStore.detail.condition }}</NTag>
        <NTag v-for="c in hwStore.detail.categories" :key="c.id" size="small">{{ c.name }}</NTag>
        <NTag v-for="t in hwStore.detail.tags" :key="t.id" size="small" type="success">{{ t.name }}</NTag>
      </NSpace>
      <div style="margin-top: 8px">
        <NSpace size="small">
          <NTag size="small">Last used: {{ hwStore.detail.lastUsedAt ? new Date(hwStore.detail.lastUsedAt).toLocaleString() : 'never' }}</NTag>
          <NTag size="small">Last activity: {{ hwStore.detail.lastActivityAt ? new Date(hwStore.detail.lastActivityAt).toLocaleString() : 'never' }}</NTag>
          <NTag v-if="hwStore.detail.currentConfigs.length" size="small" type="info">
            {{ hwStore.detail.currentConfigs[0].kind }}: {{ hwStore.detail.currentConfigs[0].name }} {{ hwStore.detail.currentConfigs[0].version || '' }}
          </NTag>
          <NTag v-if="hwStore.detail.activeLoan" size="small" type="warning">Loaned to {{ hwStore.detail.activeLoan.loanedTo }}</NTag>
        </NSpace>
      </div>
      <div style="margin-top: 12px">
        <NSpace>
          <NButton size="small" @click="markUsed">Mark used today</NButton>
          <RouterLink to="/hardware"><NButton size="small">Back to list</NButton></RouterLink>
        </NSpace>
      </div>
    </NCard>

    <NCard>
      <NTabs type="line" animated>
        <NTabPane name="specs" tab="Specs & IDs">
          <NDescriptions :column="2" bordered>
            <NDescriptionsItem label="Manufacturer">{{ hwStore.detail.manufacturer || '—' }}</NDescriptionsItem>
            <NDescriptionsItem label="Model">{{ hwStore.detail.model || '—' }}</NDescriptionsItem>
            <NDescriptionsItem label="Serial #">{{ hwStore.detail.serialNumber || '—' }}</NDescriptionsItem>
            <NDescriptionsItem label="SKU">{{ hwStore.detail.sku || '—' }}</NDescriptionsItem>
            <NDescriptionsItem label="Asset tag">{{ hwStore.detail.assetTag || '—' }}</NDescriptionsItem>
            <NDescriptionsItem label="Revision">{{ hwStore.detail.revision || '—' }}</NDescriptionsItem>
            <NDescriptionsItem label="Location">{{ hwStore.detail.location || '—' }}</NDescriptionsItem>
            <NDescriptionsItem label="Acquired">{{ hwStore.detail.acquiredAt ? new Date(hwStore.detail.acquiredAt).toLocaleDateString() : '—' }}</NDescriptionsItem>
          </NDescriptions>
          <h4>Specs (JSON)</h4>
          <pre style="background: var(--n-color); padding: 8px; border-radius: 4px; overflow: auto;">{{ JSON.stringify(hwStore.detail.specs ?? {}, null, 2) }}</pre>
          <h4>Identifiers (JSON)</h4>
          <pre style="background: var(--n-color); padding: 8px; border-radius: 4px; overflow: auto;">{{ JSON.stringify(hwStore.detail.identifiers ?? {}, null, 2) }}</pre>
        </NTabPane>

        <NTabPane name="activities" tab="Activities">
          <NForm inline label-placement="left" style="margin-bottom: 12px">
            <NFormItem label="Kind">
              <NSelect :options="kinds" v-model:value="activityKind" style="width: 140px" />
            </NFormItem>
            <NFormItem label="Description">
              <NInput v-model:value="activityDesc" style="width: 240px" />
            </NFormItem>
            <NFormItem label="When">
              <NDatePicker type="datetime" v-model:value="activityWhen" />
            </NFormItem>
            <NButton type="primary" @click="logActivity">Log</NButton>
          </NForm>

          <NTimeline>
            <NTimelineItem v-for="a in hwStore.detail.recentActivities" :key="a.id"
              :type="a.kind === 'used' || a.kind === 'flashed' ? 'success' : 'default'"
              :title="a.kind"
              :content="a.description ?? ''"
              :time="new Date(a.occurredAt).toLocaleString()"
            />
            <NEmpty v-if="!hwStore.detail.recentActivities.length" description="No activities yet" />
          </NTimeline>
        </NTabPane>

        <NTabPane name="configs" tab="Configs">
          <NForm inline label-placement="left" style="margin-bottom: 12px">
            <NFormItem label="Kind"><NSelect :options="configKinds" v-model:value="configKind" style="width: 140px" /></NFormItem>
            <NFormItem label="Name"><NInput v-model:value="configName" style="width: 200px" placeholder="Raspberry Pi OS Lite" /></NFormItem>
            <NFormItem label="Version"><NInput v-model:value="configVersion" style="width: 120px" /></NFormItem>
            <NButton type="primary" @click="recordConfig">Record</NButton>
          </NForm>
          <NSpace vertical size="small">
            <NCard v-for="c in hwStore.detail.currentConfigs" :key="c.id" size="small">
              <NSpace align="center">
                <NTag type="success">current</NTag>
                <NTag>{{ c.kind }}</NTag>
                <strong>{{ c.name }}</strong>
                <span v-if="c.version">v{{ c.version }}</span>
                <span v-if="c.installedAt">· installed {{ new Date(c.installedAt).toLocaleDateString() }}</span>
              </NSpace>
            </NCard>
            <NEmpty v-if="!hwStore.detail.currentConfigs.length" description="No current firmware/OS recorded" />
          </NSpace>
        </NTabPane>

        <NTabPane name="projects" tab="Projects">
          <NForm inline label-placement="left" style="margin-bottom: 12px">
            <NFormItem label="Project"><NSelect :options="projectOptions()" v-model:value="linkProjectId" style="width: 240px" /></NFormItem>
            <NFormItem label="Role"><NInput v-model:value="linkRole" style="width: 160px" /></NFormItem>
            <NButton type="primary" @click="linkProject" :disabled="!linkProjectId">Link</NButton>
          </NForm>
          <NSpace vertical size="small">
            <NCard v-for="p in hwStore.detail.projects" :key="p.projectId" size="small">
              <NSpace align="center">
                <RouterLink :to="`/projects/${p.projectId}`"><strong>{{ p.projectTitle }}</strong></RouterLink>
                <NTag v-if="p.role" size="small">{{ p.role }}</NTag>
                <NButton size="tiny" type="error" @click="unlink(p.projectId)">Unlink</NButton>
              </NSpace>
            </NCard>
            <NEmpty v-if="!hwStore.detail.projects.length" description="Not linked to any project" />
          </NSpace>
        </NTabPane>

        <NTabPane name="loans" tab="Loans">
          <NForm inline label-placement="left" style="margin-bottom: 12px" v-if="!hwStore.detail.activeLoan">
            <NFormItem label="Loaned to"><NInput v-model:value="loanTo" style="width: 200px" /></NFormItem>
            <NFormItem label="Due"><NDatePicker type="date" v-model:value="loanDue" /></NFormItem>
            <NButton type="primary" @click="startLoan">Open loan</NButton>
          </NForm>
          <NCard v-if="hwStore.detail.activeLoan" size="small">
            <NSpace>
              <NTag type="warning">open</NTag>
              <span>Loaned to <strong>{{ hwStore.detail.activeLoan.loanedTo }}</strong></span>
              <span v-if="hwStore.detail.activeLoan.dueAt">due {{ new Date(hwStore.detail.activeLoan.dueAt).toLocaleDateString() }}</span>
              <NButton size="tiny" type="primary" @click="returnLoan(hwStore.detail.activeLoan.id)">Mark returned</NButton>
            </NSpace>
          </NCard>
          <NEmpty v-else description="No active loan" />
        </NTabPane>

        <NTabPane name="links" tab="Links">
          <NSpace vertical>
            <a v-for="l in (hwStore.detail.links ?? [])" :key="l.url" :href="l.url" target="_blank" rel="noopener">
              {{ l.label }} <span style="color: var(--n-text-color-3); font-size: 12px;">({{ l.kind ?? 'link' }})</span>
            </a>
            <NEmpty v-if="!(hwStore.detail.links?.length)" description="No links" />
          </NSpace>
        </NTabPane>

        <NTabPane name="notes" tab="Notes">
          <div style="white-space: pre-wrap; font-family: monospace; font-size: 13px;">
            {{ hwStore.detail.notes || '— no notes —' }}
          </div>
        </NTabPane>
      </NTabs>
    </NCard>
  </NSpace>
</template>
