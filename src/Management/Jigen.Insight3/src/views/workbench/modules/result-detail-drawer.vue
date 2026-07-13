<script setup lang="ts">
import { computed } from 'vue';
import { useAppStore } from '@/store/modules/app';
import { decodeKey } from '@/utils/key-codec';
import { $t } from '@/locales';
import type { ResultRow } from './results-table.vue';

defineOptions({
  name: 'ResultDetailDrawer'
});

const visible = defineModel<boolean>('visible', { default: false });

interface Props {
  row: ResultRow | null;
}

const props = defineProps<Props>();

const appStore = useAppStore();
const drawerWidth = computed(() => (appStore.isMobile ? '92vw' : '38vw'));

const decodedKey = computed(() => (props.row ? decodeKey(props.row.key) : ''));

const prettyContent = computed(() => {
  if (!props.row) return '';

  try {
    return JSON.stringify(props.row.content, null, 2);
  } catch {
    return String(props.row.content);
  }
});
</script>

<template>
  <NDrawer v-model:show="visible" :width="drawerWidth">
    <NDrawerContent :title="$t('page.workbench.detail.title')" closable>
      <div v-if="row" class="flex-col gap-16px">
        <NDescriptions :column="1" label-placement="left" size="small">
          <NDescriptionsItem :label="$t('page.workbench.results.collection')">
            {{ row.collection }}
          </NDescriptionsItem>
          <NDescriptionsItem :label="$t('page.workbench.results.key')">{{ decodedKey }}</NDescriptionsItem>
          <NDescriptionsItem :label="$t('page.workbench.results.score')">
            {{ row.score.toFixed(4) }}
          </NDescriptionsItem>
        </NDescriptions>
        <div class="flex-col gap-8px">
          <span class="text-14px font-600">{{ $t('page.workbench.results.content') }}</span>
          <NCode :code="prettyContent" language="json" word-wrap show-line-numbers />
        </div>
      </div>
    </NDrawerContent>
  </NDrawer>
</template>
