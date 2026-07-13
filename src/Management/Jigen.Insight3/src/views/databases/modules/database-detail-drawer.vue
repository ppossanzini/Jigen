<script setup lang="ts">
import { computed } from 'vue';
import type { DatabaseDetails, DatabaseUserInfo } from '@/service/api-types';
import { formatBytes, formatCount, toNum } from '@/utils/format';
import { useAppStore } from '@/store/modules/app';
import { $t } from '@/locales';
import StorageBreakdownChart from './storage-breakdown-chart.vue';
import DatabaseUsersEditor from './database-users-editor.vue';

defineOptions({
  name: 'DatabaseDetailDrawer'
});

const visible = defineModel<boolean>('visible', { default: false });

const appStore = useAppStore();
// drawer width in viewport units (rule 3.5): full-width sheet on mobile, ~2/5 of the viewport otherwise
const drawerWidth = computed(() => (appStore.isMobile ? '92vw' : '38vw'));

interface Props {
  details: DatabaseDetails | null;
  loading: boolean;
}

const props = defineProps<Props>();

interface Emits {
  (e: 'usersUpdated', users: DatabaseUserInfo[]): void;
}

const emit = defineEmits<Emits>();

const collections = computed(() => props.details?.collections ?? []);

const collectionColumns = [
  { title: () => $t('page.collections.table.name'), key: 'name' },
  {
    title: () => $t('page.collections.table.vectors'),
    key: 'vectors',
    render: (row: (typeof collections.value)[number]) => formatCount(row.vectors)
  },
  {
    title: () => $t('page.collections.table.contentSize'),
    key: 'contentSize',
    render: (row: (typeof collections.value)[number]) => formatBytes(row.contentSize)
  },
  {
    title: () => $t('page.collections.table.vectorSize'),
    key: 'vectorSize',
    render: (row: (typeof collections.value)[number]) => formatBytes(row.vectorSize)
  }
];
</script>

<template>
  <NDrawer v-model:show="visible" :width="drawerWidth">
    <NDrawerContent :title="$t('page.databases.detail.title')" closable>
      <NSkeleton v-if="loading" text :repeat="6" />
      <NEmpty v-else-if="!details" :description="$t('common.noData')" />
      <div v-else class="flex-col gap-24px">
        <NDescriptions :column="1" label-placement="left" size="small">
          <NDescriptionsItem :label="$t('page.databases.table.name')">{{ details.name }}</NDescriptionsItem>
          <NDescriptionsItem :label="$t('page.databases.table.created')">
            {{ details.createdAtUtc ? new Date(details.createdAtUtc).toLocaleString() : '—' }}
          </NDescriptionsItem>
          <NDescriptionsItem :label="$t('page.databases.table.vectors')">
            {{ formatCount(details.vectors) }}
          </NDescriptionsItem>
          <NDescriptionsItem :label="$t('page.databases.table.freeSpace')">
            {{ formatBytes(toNum(details.contentFreeSpace) + toNum(details.vectorFreeSpace)) }}
          </NDescriptionsItem>
        </NDescriptions>

        <div class="flex-col gap-8px">
          <span class="text-14px font-600">{{ $t('page.databases.detail.storageBreakdown') }}</span>
          <div class="min-h-240px">
            <NEmpty v-if="!collections.length" :description="$t('page.databases.detail.noCollections')" class="pt-40px" />
            <StorageBreakdownChart v-else :collections="collections" />
          </div>
        </div>

        <div class="flex-col gap-8px">
          <span class="text-14px font-600">{{ $t('page.databases.detail.collectionsSummary') }}</span>
          <NEmpty v-if="!collections.length" :description="$t('page.databases.detail.noCollections')" />
          <NDataTable
            v-else
            :columns="collectionColumns"
            :data="collections"
            :bordered="false"
            size="small"
            :max-height="240"
            virtual-scroll
          />
        </div>

        <div class="flex-col gap-8px">
          <span class="text-14px font-600">{{ $t('page.databases.detail.usersTitle') }}</span>
          <DatabaseUsersEditor
            :database="details.name ?? ''"
            :users="details.users ?? []"
            @updated="emit('usersUpdated', $event)"
          />
        </div>
      </div>
    </NDrawerContent>
  </NDrawer>
</template>
