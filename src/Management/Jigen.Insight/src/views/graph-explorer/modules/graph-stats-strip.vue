<script setup lang="ts">
import { computed } from 'vue';
import type { IndexGraphSnapshot } from '@/service/api-types';
import { formatCount } from '@/utils/format';
import { $t } from '@/locales';

defineOptions({
  name: 'GraphStatsStrip'
});

interface Props {
  snapshot: IndexGraphSnapshot;
}

const props = defineProps<Props>();

const stats = computed(() => [
  { label: $t('page.graph-explorer.stats.total'), value: formatCount(props.snapshot.totalNodes) },
  { label: $t('page.graph-explorer.stats.live'), value: formatCount(props.snapshot.liveNodes) },
  { label: $t('page.graph-explorer.stats.deleted'), value: formatCount(props.snapshot.deletedNodes) },
  { label: $t('page.graph-explorer.stats.returned'), value: formatCount(props.snapshot.returnedNodes) },
  { label: $t('page.graph-explorer.stats.maxLevel'), value: formatCount(props.snapshot.maxLevel) },
  {
    label: $t('page.graph-explorer.stats.entrypoint'),
    value: props.snapshot.entrypointPositionId != null ? `#${formatCount(props.snapshot.entrypointPositionId)}` : '—'
  }
]);

const truncated = computed(() => Boolean(props.snapshot.truncated));
</script>

<template>
  <div class="grid grid-cols-2 gap-12px sm:grid-cols-3 lg:grid-cols-7">
    <div v-for="stat in stats" :key="stat.label" class="flex-col gap-2px">
      <span class="text-12px text-gray-500">{{ stat.label }}</span>
      <span class="text-16px font-600">{{ stat.value }}</span>
    </div>
    <div class="flex-col gap-2px">
      <span class="text-12px text-gray-500">{{ $t('page.graph-explorer.stats.truncated') }}</span>
      <NTag :type="truncated ? 'warning' : 'success'" size="small" round class="w-fit">
        {{ truncated ? $t('common.yesOrNo.yes') : $t('common.yesOrNo.no') }}
      </NTag>
    </div>
  </div>
</template>
