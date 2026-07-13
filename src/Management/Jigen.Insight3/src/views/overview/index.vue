<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useDocumentVisibility, useIntervalFn } from '@vueuse/core';
import { fetchServerStatus } from '@/service/api';
import type { ServerStatusHistory, ServerStatusSample, ServerStatusWindow } from '@/service/api-types';
import { formatBytes, formatCount, formatPercent, toNum } from '@/utils/format';
import { $t } from '@/locales';
import KpiCard from './modules/kpi-card.vue';
import MetricLineChart from './modules/metric-line-chart.vue';
import DatabaseSizesChart from './modules/database-sizes-chart.vue';
import type { SizeSeriesGroup } from './modules/database-sizes-chart.vue';

defineOptions({
  name: 'OverviewPage'
});

const POLL_INTERVAL_MS = 5000;

const WINDOWS: ServerStatusWindow[] = ['1m', '5m', '10m', '1h'];

const statusWindow = ref<ServerStatusWindow>('1m');
const history = ref<ServerStatusHistory | null>(null);
/** at least one request for the current window has settled */
const settled = ref(false);
const hasError = ref(false);

let requestSeq = 0;

async function loadData() {
  const seq = (requestSeq += 1);

  const { data, error } = await fetchServerStatus(statusWindow.value);

  // ignore responses of stale requests (window switched meanwhile)
  if (seq !== requestSeq) return;

  if (!error) {
    history.value = data;
    hasError.value = false;
  } else {
    hasError.value = true;
  }

  settled.value = true;
}

watch(statusWindow, () => {
  history.value = null;
  settled.value = false;
  hasError.value = false;
  loadData();
});

// poll while the page lives and the document is visible
const { pause, resume } = useIntervalFn(loadData, POLL_INTERVAL_MS);
const visibility = useDocumentVisibility();

watch(visibility, current => {
  if (current === 'visible') {
    loadData();
    resume();
  } else {
    pause();
  }
});

loadData();

const windowOptions = computed(() =>
  WINDOWS.map(item => ({
    label: $t(`page.overview.window.${item}`),
    value: item
  }))
);

const samples = computed<ServerStatusSample[]>(() => history.value?.samples ?? []);

const latest = computed(() => samples.value.at(-1));

interface Kpi {
  key: string;
  label: string;
  value: string;
  icon: string;
}

const kpis = computed<Kpi[]>(() => {
  const sample = latest.value;
  if (!sample) return [];

  const databases = sample.databases ?? [];

  const collections = databases.reduce((acc, db) => acc + toNum(db.collectionsCount), 0);
  const vectors = databases.reduce((acc, db) => acc + toNum(db.totalElementsCount), 0);
  const queue = databases.reduce((acc, db) => acc + toNum(db.ingestionQueueLength), 0);

  return [
    {
      key: 'cpu',
      label: $t('page.overview.kpi.cpu'),
      value: formatPercent(sample.cpuUsagePercent),
      icon: 'mdi:cpu-64-bit'
    },
    {
      key: 'memory',
      label: $t('page.overview.kpi.memory'),
      value: formatBytes(sample.memoryUsageBytes),
      icon: 'mdi:memory'
    },
    {
      key: 'databases',
      label: $t('page.overview.kpi.databases'),
      value: formatCount(databases.length),
      icon: 'mdi:database-outline'
    },
    {
      key: 'collections',
      label: $t('page.overview.kpi.collections'),
      value: formatCount(collections),
      icon: 'mdi:folder-multiple-outline'
    },
    {
      key: 'vectors',
      label: $t('page.overview.kpi.vectors'),
      value: formatCount(vectors),
      icon: 'mdi:chart-scatter-plot'
    },
    {
      key: 'queue',
      label: $t('page.overview.kpi.ingestionQueue'),
      value: formatCount(queue),
      icon: 'mdi:tray-full'
    }
  ];
});

function sampleTime(sample: ServerStatusSample): number {
  return Date.parse(sample.timestampUtc ?? '') || 0;
}

const cpuPoints = computed<[number, number][]>(() => samples.value.map(s => [sampleTime(s), toNum(s.cpuUsagePercent)]));

const memoryPoints = computed<[number, number][]>(() =>
  samples.value.map(s => [sampleTime(s), toNum(s.memoryUsageBytes)])
);

const queuePoints = computed<[number, number][]>(() =>
  samples.value.map(s => [
    sampleTime(s),
    (s.databases ?? []).reduce((acc, db) => acc + toNum(db.ingestionQueueLength), 0)
  ])
);

const sizeGroups = computed<SizeSeriesGroup[]>(() => {
  const kinds = [
    { kind: 'content', prop: 'contentSizeBytes' },
    { kind: 'vector', prop: 'vectorSizeBytes' },
    { kind: 'index', prop: 'indexSizeBytes' }
  ] as const;

  const databaseNames = [...new Set(samples.value.flatMap(s => (s.databases ?? []).map(db => db.name ?? '')))].filter(
    Boolean
  );

  return databaseNames.flatMap(name =>
    kinds.map(({ kind, prop }) => ({
      database: name,
      kind,
      points: samples.value.map<[number, number]>(s => {
        const db = (s.databases ?? []).find(item => item.name === name);

        return [sampleTime(s), toNum(db?.[prop])];
      })
    }))
  );
});
</script>

<template>
  <div class="flex-col gap-16px">
    <NCard :bordered="false" size="small" class="card-wrapper">
      <div class="flex-y-center flex-wrap justify-between gap-12px">
        <span class="text-16px font-600">{{ $t('page.overview.title') }}</span>
        <div class="flex-y-center gap-8px">
          <span class="text-12px text-gray-500">{{ $t('page.overview.window.title') }}</span>
          <NRadioGroup v-model:value="statusWindow" size="small">
            <NRadioButton v-for="option in windowOptions" :key="option.value" :value="option.value">
              {{ option.label }}
            </NRadioButton>
          </NRadioGroup>
        </div>
      </div>
    </NCard>

    <NAlert v-if="hasError && history" type="warning" :show-icon="true" :bordered="false">
      {{ $t('page.overview.state.connectionLost') }}
    </NAlert>

    <!-- initial load -->
    <NCard v-if="!settled" :bordered="false" class="card-wrapper flex-1">
      <NSpace vertical :size="16">
        <NSkeleton height="80px" :sharp="false" />
        <NSkeleton height="240px" :sharp="false" />
        <NSkeleton height="240px" :sharp="false" />
      </NSpace>
    </NCard>

    <!-- failed and nothing to show -->
    <NCard v-else-if="hasError && !history" :bordered="false" class="card-wrapper flex-1">
      <div class="size-full min-h-320px flex-center">
        <NResult
          status="error"
          :title="$t('page.overview.state.loadFailed')"
          :description="$t('request.serverUnreachable')"
        >
          <template #footer>
            <NButton type="primary" @click="loadData">{{ $t('common.refresh') }}</NButton>
          </template>
        </NResult>
      </div>
    </NCard>

    <!-- loaded but no samples -->
    <NCard v-else-if="!samples.length" :bordered="false" class="card-wrapper flex-1">
      <div class="size-full min-h-320px flex-center">
        <NEmpty :description="$t('common.noData')" />
      </div>
    </NCard>

    <template v-else>
      <NGrid :x-gap="16" :y-gap="16" responsive="screen" item-responsive>
        <NGi v-for="kpi in kpis" :key="kpi.key" span="12 s:8 l:4">
          <KpiCard :label="kpi.label" :value="kpi.value" :icon="kpi.icon" />
        </NGi>
      </NGrid>

      <div class="grid grid-cols-1 flex-1 auto-rows-fr gap-16px xl:grid-cols-2">
        <NCard
          :bordered="false"
          size="small"
          class="card-wrapper h-full min-h-280px flex-col"
          content-class="flex-1 min-h-0"
          :title="$t('page.overview.charts.cpu')"
        >
          <MetricLineChart
            :series-name="$t('page.overview.charts.cpu')"
            :points="cpuPoints"
            unit="percent"
            :color-index="0"
          />
        </NCard>
        <NCard
          :bordered="false"
          size="small"
          class="card-wrapper h-full min-h-280px flex-col"
          content-class="flex-1 min-h-0"
          :title="$t('page.overview.charts.memory')"
        >
          <MetricLineChart
            :series-name="$t('page.overview.charts.memory')"
            :points="memoryPoints"
            unit="bytes"
            :color-index="1"
          />
        </NCard>
        <NCard
          :bordered="false"
          size="small"
          class="card-wrapper h-full min-h-280px flex-col"
          content-class="flex-1 min-h-0"
          :title="$t('page.overview.charts.ingestionQueue')"
        >
          <MetricLineChart
            :series-name="$t('page.overview.charts.ingestionQueue')"
            :points="queuePoints"
            unit="count"
            :color-index="3"
          />
        </NCard>
        <NCard
          :bordered="false"
          size="small"
          class="card-wrapper h-full min-h-280px flex-col"
          content-class="flex-1 min-h-0"
          :title="$t('page.overview.charts.databaseSizes')"
        >
          <DatabaseSizesChart :groups="sizeGroups" />
        </NCard>
      </div>
    </template>
  </div>
</template>
