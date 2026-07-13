<script setup lang="ts">
import { computed } from 'vue';
import { useAppStore } from '@/store/modules/app';
import { useRouterPush } from '@/hooks/common/router';
import type { CollectionInfo } from '@/service/api-types';
import { formatBytes, formatCount, toNum } from '@/utils/format';
import { $t } from '@/locales';

defineOptions({
  name: 'CollectionDetailDrawer'
});

const visible = defineModel<boolean>('visible', { default: false });

interface Props {
  database: string;
  info: CollectionInfo | null;
  loading: boolean;
}

const props = defineProps<Props>();

const appStore = useAppStore();
const drawerWidth = computed(() => (appStore.isMobile ? '92vw' : '38vw'));

const { routerPushByKey } = useRouterPush();

const index = computed(() => props.info?.index ?? null);

function openInWorkbench() {
  routerPushByKey('workbench', {
    query: { db: props.database, collection: props.info?.name ?? '' }
  });
}

function openInGraphExplorer() {
  routerPushByKey('graph-explorer', {
    query: { db: props.database, collection: props.info?.name ?? '' }
  });
}
</script>

<template>
  <NDrawer v-model:show="visible" :width="drawerWidth">
    <NDrawerContent :title="$t('page.collections.detail.title')" closable>
      <NSkeleton v-if="loading" text :repeat="6" />
      <NEmpty v-else-if="!info" :description="$t('common.noData')" />
      <div v-else class="flex-col gap-24px">
        <NDescriptions :column="1" label-placement="left" size="small">
          <NDescriptionsItem :label="$t('page.collections.table.name')">{{ info.name }}</NDescriptionsItem>
          <NDescriptionsItem :label="$t('page.collections.table.vectors')">
            {{ formatCount(info.vectors) }}
          </NDescriptionsItem>
          <NDescriptionsItem :label="$t('page.collections.table.dimensions')">
            {{ formatCount(info.dimensions) }}
          </NDescriptionsItem>
          <NDescriptionsItem :label="$t('page.collections.table.contentSize')">
            {{ formatBytes(info.contentSize) }}
          </NDescriptionsItem>
          <NDescriptionsItem :label="$t('page.collections.table.vectorSize')">
            {{ formatBytes(info.vectorSize) }}
          </NDescriptionsItem>
        </NDescriptions>

        <div class="flex-col gap-8px">
          <span class="text-14px font-600">{{ $t('page.collections.detail.indexTitle') }}</span>
          <NEmpty v-if="!index" :description="$t('page.collections.detail.noIndex')" />
          <NDescriptions v-else :column="1" label-placement="left" size="small">
            <NDescriptionsItem :label="$t('page.collections.table.maxLevel')">
              {{ formatCount(index.maxLevel) }}
            </NDescriptionsItem>
            <NDescriptionsItem :label="$t('page.collections.table.averageDegree')">
              {{ toNum(index.averageDegree).toFixed(2) }}
            </NDescriptionsItem>
            <NDescriptionsItem :label="$t('page.collections.table.deletedCount')">
              {{ formatCount(index.deletedNodes) }}
            </NDescriptionsItem>
            <NDescriptionsItem :label="$t('page.collections.table.quantization')">
              {{ index.quantization ?? '—' }}
            </NDescriptionsItem>
            <NDescriptionsItem :label="$t('page.collections.table.indexSize')">
              {{ formatBytes(index.indexSizeBytes) }}
            </NDescriptionsItem>
          </NDescriptions>
        </div>

        <div class="flex flex-wrap gap-12px">
          <NButton type="primary" @click="openInWorkbench">
            <template #icon><SvgIcon icon="mdi:database-search-outline" /></template>
            {{ $t('page.collections.detail.openInWorkbench') }}
          </NButton>
          <NButton @click="openInGraphExplorer">
            <template #icon><SvgIcon icon="mdi:graph-outline" /></template>
            {{ $t('page.collections.detail.openInGraphExplorer') }}
          </NButton>
        </div>
      </div>
    </NDrawerContent>
  </NDrawer>
</template>
