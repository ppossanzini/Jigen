<script setup lang="ts">
import { ref } from 'vue';
import { $t } from '@/locales';
import UsersPanel from './modules/users-panel.vue';
import RolesPanel from './modules/roles-panel.vue';
import AppsPanel from './modules/apps-panel.vue';

defineOptions({
  name: 'SecurityPage'
});

const activeTab = ref<'users' | 'roles' | 'apps'>('users');
</script>

<template>
  <div class="h-full flex-col gap-16px">
    <NCard :bordered="false" size="small" class="card-wrapper">
      <span class="text-16px font-600">{{ $t('page.security.title') }}</span>
    </NCard>

    <NCard :bordered="false" size="small" class="card-wrapper min-h-0 flex-1" content-class="h-full min-h-0 flex-col">
      <NTabs
        v-model:value="activeTab"
        type="line"
        animated
        class="h-full flex-col"
        pane-wrapper-class="min-h-0 flex-1 flex-col"
        pane-class="h-full min-h-0 flex-col"
      >
        <NTabPane name="users" :tab="$t('page.security.tabs.users')" display-directive="show">
          <UsersPanel v-if="activeTab === 'users'" />
        </NTabPane>
        <NTabPane name="roles" :tab="$t('page.security.tabs.roles')" display-directive="show">
          <RolesPanel v-if="activeTab === 'roles'" />
        </NTabPane>
        <NTabPane name="apps" :tab="$t('page.security.tabs.apps')" display-directive="show">
          <AppsPanel v-if="activeTab === 'apps'" />
        </NTabPane>
      </NTabs>
    </NCard>
  </div>
</template>
