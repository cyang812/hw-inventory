import { createRouter, createWebHistory } from 'vue-router';
import Dashboard from '../views/Dashboard.vue';
import HardwareList from '../views/HardwareList.vue';
import HardwareDetail from '../views/HardwareDetail.vue';
import ProjectList from '../views/ProjectList.vue';
import ProjectDetail from '../views/ProjectDetail.vue';
import ProjectTimeline from '../views/ProjectTimeline.vue';
import Settings from '../views/Settings.vue';

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'dashboard', component: Dashboard },
    { path: '/hardware', name: 'hardware', component: HardwareList },
    { path: '/hardware/:id(\\d+)', name: 'hardware-detail', component: HardwareDetail, props: true },
    { path: '/projects', name: 'projects', component: ProjectList },
    { path: '/projects/timeline', name: 'projects-timeline', component: ProjectTimeline },
    { path: '/projects/:id(\\d+)', name: 'project-detail', component: ProjectDetail, props: true },
    { path: '/settings', name: 'settings', component: Settings },
  ],
});
