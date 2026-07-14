<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { fetchCollectionGraph, fetchListCollections } from '@/service/api';
import type { IndexGraphSnapshot } from '@/service/api-types';
import { useDatabaseStore } from '@/store/modules/database';
import { toNum } from '@/utils/format';
import { $t } from '@/locales';
import GraphStatsStrip from './modules/graph-stats-strip.vue';
import Graph2DChart from './modules/graph-2d-chart.vue';
import Graph3DChart from './modules/graph-3d-chart.vue';
import NodeDetailDrawer from './modules/node-detail-drawer.vue';
import type { PreparedNode } from './modules/graph-data';

defineOptions({
  name: 'GraphExplorerPage'
});

const databaseStore = useDatabaseStore();
const route = useRoute();

const databaseOptions = computed(() => databaseStore.databases.map(name => ({ label: name, value: name })));

const collectionOptions = ref<string[]>([]);
const selectedCollection = ref('');

async function loadCollections() {
  selectedCollection.value = '';
  collectionOptions.value = [];

  if (!databaseStore.current) return;

  const { data, error } = await fetchListCollections(databaseStore.current);
  if (!error) {
    collectionOptions.value = data ?? [];
  }
}

watch(() => databaseStore.current, loadCollections);

const dimensions = ref<2 | 3>(2);
const limit = ref(500);
const levelFilter = ref<number | null>(null);

const loading = ref(false);
const hasError = ref(false);
const errorMessage = ref('');
const settled = ref(false);
const snapshot = ref<IndexGraphSnapshot | null>(null);

async function loadGraph() {
  if (!databaseStore.current || !selectedCollection.value) return;

  loading.value = true;
  hasError.value = false;
  errorMessage.value = '';

  const { data, error } = await fetchCollectionGraph(databaseStore.current, selectedCollection.value, {
    dimensions: dimensions.value,
    limit: limit.value,
    level: levelFilter.value ?? undefined
  });

  loading.value = false;
  settled.value = true;

  if (error) {
    hasError.value = true;
    errorMessage.value =
      error.response?.data && typeof error.response.data === 'string' ? error.response.data : $t('request.serverUnreachable');
    snapshot.value = null;
    return;
  }

  snapshot.value = data;
}

watch(dimensions, () => {
  if (snapshot.value && databaseStore.current && selectedCollection.value) {
    loadGraph();
  }
});

onMounted(async () => {
  if (!databaseStore.loaded) {
    await databaseStore.loadDatabases();
  }

  const queryDb = route.query.db;
  const deepLinkedDb = typeof queryDb === 'string' && databaseStore.databases.includes(queryDb);
  if (deepLinkedDb) {
    databaseStore.setCurrent(queryDb as string);
  }

  await loadCollections();

  const queryCollection = route.query.collection;
  const deepLinkedCollection = typeof queryCollection === 'string' && collectionOptions.value.includes(queryCollection);
  if (deepLinkedCollection) {
    selectedCollection.value = queryCollection as string;
  }

  if (deepLinkedDb && deepLinkedCollection) {
    loadGraph();
  }
});

const hasNodes = computed(() => Boolean(snapshot.value?.nodes?.length));
const snapshotIs3d = computed(() => toNum(snapshot.value?.dimensions) === 3);

const nodeDetailVisible = ref(false);
const selectedNode = ref<PreparedNode | null>(null);

function openNodeDetail(node: PreparedNode) {
  selectedNode.value = node;
  nodeDetailVisible.value = true;
}
</script>

<template>
  <div class="h-full flex-col gap-16px">
    <NCard :bordered="false" size="small" class="card-wrapper">
      <template v-if="snapshot" #header-extra>
        <GraphStatsStrip :snapshot="snapshot" />
      </template>
      <div class="grid grid-cols-1 gap-12px md:grid-cols-2 xl:grid-cols-6">
        <NFormItem :label="$t('page.graph-explorer.controls.database')" :show-feedback="false">
          <NSelect
            :value="databaseStore.current || null"
            :options="databaseOptions"
            :loading="databaseStore.loading"
            :placeholder="$t('page.graph-explorer.controls.databasePlaceholder')"
            @update:value="(value: string) => databaseStore.setCurrent(value)"
          />
        </NFormItem>
        <NFormItem :label="$t('page.graph-explorer.controls.collection')" :show-feedback="false">
          <NSelect
            v-model:value="selectedCollection"
            filterable
            :options="collectionOptions.map(name => ({ label: name, value: name }))"
            :placeholder="$t('page.graph-explorer.controls.collectionPlaceholder')"
          />
        </NFormItem>
        <NFormItem :label="$t('page.graph-explorer.controls.dimensions')" :show-feedback="false">
          <NRadioGroup v-model:value="dimensions">
            <NRadioButton :value="2">{{ $t('page.graph-explorer.controls.dimensions2d') }}</NRadioButton>
            <NRadioButton :value="3">{{ $t('page.graph-explorer.controls.dimensions3d') }}</NRadioButton>
          </NRadioGroup>
        </NFormItem>
        <NFormItem :label="$t('page.graph-explorer.controls.limit')" :show-feedback="false">
          <NInputNumber v-model:value="limit" :min="1" :max="20000" class="w-full" />
        </NFormItem>
        <NFormItem :label="$t('page.graph-explorer.controls.level')" :show-feedback="false">
          <div class="flex-y-center w-full gap-8px">
            <NInputNumber
              v-model:value="levelFilter"
              :min="0"
              :placeholder="$t('page.graph-explorer.controls.levelPlaceholder')"
              class="min-w-0 flex-1"
            />
            <NButton v-if="levelFilter !== null" quaternary size="small" @click="levelFilter = null">
              {{ $t('page.graph-explorer.controls.levelClear') }}
            </NButton>
          </div>
        </NFormItem>
        <NFormItem :show-feedback="false" label=" ">
          <NButton
            type="primary"
            class="w-full"
            :loading="loading"
            :disabled="!databaseStore.current || !selectedCollection"
            @click="loadGraph"
          >
            <template #icon><SvgIcon icon="mdi:graph-outline" /></template>
            {{ $t('page.graph-explorer.controls.load') }}
          </NButton>
        </NFormItem>
      </div>
    </NCard>

    <NCard :bordered="false" size="small" class="card-wrapper min-h-0 flex-1" content-class="h-full min-h-0 flex-col">
      <NEmpty
        v-if="!databaseStore.current"
        :description="$t('page.graph-explorer.empty.noDatabaseSelected')"
        class="flex-1 flex-center"
      />
      <NEmpty
        v-else-if="!selectedCollection"
        :description="$t('page.graph-explorer.empty.noCollectionSelected')"
        class="flex-1 flex-center"
      />
      <NResult
        v-else-if="!loading && hasError"
        status="error"
        :title="$t('page.overview.state.loadFailed')"
        :description="errorMessage"
        class="flex-1 flex-center"
      >
        <template #footer>
          <NButton type="primary" @click="loadGraph">{{ $t('common.refresh') }}</NButton>
        </template>
      </NResult>
      <div v-else-if="loading && !snapshot" class="min-h-0 flex-1 flex-col gap-12px p-16px">
        <NSkeleton text :repeat="1" class="h-24px w-240px" />
        <NSkeleton class="min-h-0 flex-1 rounded-8px" />
      </div>
      <NEmpty
        v-else-if="settled && !loading && !hasNodes"
        :description="$t('page.graph-explorer.empty.noNodes')"
        class="flex-1 flex-center"
      />
      <NSpin v-else-if="snapshot" :show="loading" class="min-h-0 flex-1" content-class="h-full">
        <Graph2DChart v-if="!snapshotIs3d" :snapshot="snapshot" @node-click="openNodeDetail" />
        <Graph3DChart v-else :snapshot="snapshot" @node-click="openNodeDetail" />
      </NSpin>
    </NCard>

    <NodeDetailDrawer
      v-model:visible="nodeDetailVisible"
      :database="databaseStore.current ?? ''"
      :collection="selectedCollection ?? ''"
      :node="selectedNode"
    />
  </div>
</template>
