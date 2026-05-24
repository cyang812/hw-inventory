<script setup lang="ts">
import { ref } from 'vue';
import { RouterView, useRouter } from 'vue-router';
import {
  NConfigProvider, NLayout, NLayoutHeader, NLayoutSider, NMenu, NIcon, NSpace, NButton, NInput, NPopover,
  NMessageProvider, NDialogProvider, NNotificationProvider, NLoadingBarProvider,
  darkTheme,
} from 'naive-ui';
import { api } from './api/client';

const router = useRouter();
const dark = ref(false);
const tokenInput = ref('');
const showTokenPopover = ref(false);

const menuOptions = [
  { label: 'Dashboard', key: 'dashboard' },
  { label: 'Hardware', key: 'hardware' },
  { label: 'Projects', key: 'projects' },
  { label: 'Timeline', key: 'projects-timeline' },
  { label: 'Settings', key: 'settings' },
];

function handleSelect(key: string) {
  router.push({ name: key });
}

function saveToken() {
  api.setToken(tokenInput.value || null);
  showTokenPopover.value = false;
  location.reload();
}
</script>

<template>
  <NConfigProvider :theme="dark ? darkTheme : null">
    <NLoadingBarProvider>
      <NDialogProvider>
        <NNotificationProvider>
          <NMessageProvider>
            <NLayout style="height: 100vh">
              <NLayoutHeader bordered style="padding: 12px 24px; display: flex; align-items: center; gap: 16px;">
                <h2 style="margin: 0; font-weight: 600;">hw-inventory</h2>
                <span style="color: var(--n-text-color-3); font-size: 12px;">personal hardware tracker</span>
                <div style="flex: 1" />
                <NPopover trigger="manual" :show="showTokenPopover" @update:show="showTokenPopover = $event" placement="bottom-end">
                  <template #trigger>
                    <NButton size="small" @click="showTokenPopover = !showTokenPopover">Auth</NButton>
                  </template>
                  <NSpace vertical>
                    <span>Bearer token (leave blank to clear)</span>
                    <NInput v-model:value="tokenInput" type="password" placeholder="paste token" />
                    <NButton size="small" type="primary" @click="saveToken">Save</NButton>
                  </NSpace>
                </NPopover>
                <NButton size="small" :type="dark ? 'primary' : 'default'" @click="dark = !dark">{{ dark ? 'Light' : 'Dark' }}</NButton>
              </NLayoutHeader>
              <NLayout has-sider style="height: calc(100vh - 56px)">
                <NLayoutSider bordered collapse-mode="width" :collapsed-width="64" :width="200" :native-scrollbar="false">
                  <NMenu :options="menuOptions" :value="String(router.currentRoute.value.name ?? 'dashboard')" @update:value="handleSelect" />
                </NLayoutSider>
                <NLayout :native-scrollbar="false" content-style="padding: 24px;">
                  <RouterView />
                </NLayout>
              </NLayout>
            </NLayout>
          </NMessageProvider>
        </NNotificationProvider>
      </NDialogProvider>
    </NLoadingBarProvider>
  </NConfigProvider>
</template>
