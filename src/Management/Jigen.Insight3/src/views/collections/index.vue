<script setup lang="ts">
import { computed, h, onMounted, ref, watch } from 'vue';
import type { DataTableColumns } from 'naive-ui';
import { useRoute } from 'vue-router';
import { fetchCollectionInfo, fetchListCollections } from '@/service/api';
import type { CollectionInfo } from '@/service/api-types';
import { useDatabaseStore } from '@/store/modules/database';
import { formatBytes, formatCount, toNum } from '@/utils/format';
import { $t } from '@/locales';
import CollectionDetailDrawer from './modules/collection-detail-drawer.vue';

defineOptions({
  name: 'CollectionsPage'
});

const databaseStore = useDatabaseStore();
const route = useRoute();

const rows = ref<CollectionInfo[]>([]);
const loading = ref(false);
const hasError = ref(false);
const settled = ref(false);

const databaseOptions = computed(() => databaseStore.databases.map(name => ({ label: name, value: name })));

async function loadRows() {
  const dbname = databaseStore.current;

  if (!dbname) {
    rows.value = [];
    settled.value = true;
    hasError.value = false;
    return;
  }

  loading.value = true;
  hasError.value = false;

  const { data: names, error } = await fetchListCollections(dbname);

  if (error) {
    hasError.value = true;
    loading.value = false;
    settled.value = true;
    return;
  }

  const infos = await Promise.all(
    (names ?? []).map(async name => {
      const { data, error: infoError } = await fetchCollectionInfo(dbname, name);
      return infoError ? { name } : (data as CollectionInfo);
    })
  );

  rows.value = infos;
  loading.value = false;
  settled.value = true;
}

onMounted(async () => {
  if (!databaseStore.loaded) {
    await databaseStore.loadDatabases();
  }

  // allow deep-linking with ?db=name (e.g. from the Databases page)
  const queryDb = route.query.db;
  if (typeof queryDb === 'string' && databaseStore.databases.includes(queryDb)) {
    databaseStore.setCurrent(queryDb);
  }

  loadRows();
});

watch(
  () => databaseStore.current,
  () => loadRows()
);

// --- detail drawer ---
const detailVisible = ref(false);
const detailLoading = ref(false);
const detailInfo = ref<CollectionInfo | null>(null);

async function openDetail(name: string) {
  detailVisible.value = true;
  detailLoading.value = true;
  detailInfo.value = null;

  const { data, error } = await fetchCollectionInfo(databaseStore.current, name);
  detailLoading.value = false;

  if (!error) {
    detailInfo.value = data;
  }
}

const columns: DataTableColumns<CollectionInfo> = [
  {
    title: () => $t('page.collections.table.name'),
    key: 'name',
    render: row =>
      h(
        'a',
        {
          class: 'text-primary dark:text-primary-800 cursor-pointer hover:underline',
          onClick: () => openDetail(row.name ?? '')
        },
        row.name ?? ''
      )
  },
  {
    title: () => $t('page.collections.table.vectors'),
    key: 'vectors',
    render: row => formatCount(row.vectors)
  },
  {
    title: () => $t('page.collections.table.dimensions'),
    key: 'dimensions',
    render: row => formatCount(row.dimensions)
  },
  {
    title: () => $t('page.collections.table.contentSize'),
    key: 'contentSize',
    render: row => formatBytes(row.contentSize)
  },
  {
    title: () => $t('page.collections.table.vectorSize'),
    key: 'vectorSize',
    render: row => formatBytes(row.vectorSize)
  },
  {
    title: () => $t('page.collections.table.maxLevel'),
    key: 'maxLevel',
    render: row => (row.index ? formatCount(row.index.maxLevel) : '—')
  },
  {
    title: () => $t('page.collections.table.averageDegree'),
    key: 'averageDegree',
    render: row => (row.index ? toNum(row.index.averageDegree).toFixed(2) : '—')
  },
  {
    title: () => $t('page.collections.table.deletedCount'),
    key: 'deletedCount',
    render: row => (row.index ? formatCount(row.index.deletedNodes) : '—')
  },
  {
    title: () => $t('page.collections.table.quantization'),
    key: 'quantization',
    render: row => row.index?.quantization ?? '—'
  }
];
</script>

<template>
  <div class="h-full flex-col gap-16px">
    <NCard :bordered="false" size="small" class="card-wrapper">
      <div class="flex-y-center flex-wrap gap-12px">
        <span class="text-16px font-600">{{ $t('page.collections.title') }}</span>
        <div class="flex-y-center shrink-0 gap-8px">
          <span class="shrink-0 whitespace-nowrap text-12px text-gray-500">
            {{ $t('page.collections.databaseSelector.label') }}
          </span>
          <NSelect
            :value="databaseStore.current || null"
            :options="databaseOptions"
            :loading="databaseStore.loading"
            :placeholder="$t('page.collections.databaseSelector.placeholder')"
            class="min-w-220px"
            @update:value="(value: string) => databaseStore.setCurrent(value)"
          />
        </div>
      </div>
    </NCard>

    <NCard :bordered="false" size="small" class="card-wrapper min-h-0 flex-1" content-class="h-full min-h-0 flex-col">
      <NEmpty
        v-if="!databaseStore.current"
        :description="
          databaseStore.loaded && !databaseStore.databases.length
            ? $t('page.collections.empty.noDatabases')
            : $t('page.collections.empty.noDatabaseSelected')
        "
        class="flex-1 flex-center"
      />
      <NResult
        v-else-if="!loading && hasError"
        status="error"
        :title="$t('page.overview.state.loadFailed')"
        :description="$t('request.serverUnreachable')"
        class="flex-1 flex-center"
      >
        <template #footer>
          <NButton type="primary" @click="loadRows">{{ $t('common.refresh') }}</NButton>
        </template>
      </NResult>
      <NEmpty
        v-else-if="settled && !loading && !rows.length"
        :description="$t('page.collections.empty.noCollections')"
        class="flex-1 flex-center"
      />
      <NDataTable
        v-else
        :columns="columns"
        :data="rows"
        :loading="loading"
        :bordered="false"
        :pagination="false"
        flex-height
        virtual-scroll
        class="h-full"
        :row-key="(row: CollectionInfo) => row.name ?? ''"
      />
    </NCard>

    <CollectionDetailDrawer
      v-model:visible="detailVisible"
      :database="databaseStore.current"
      :info="detailInfo"
      :loading="detailLoading"
    />
  </div>
</template>
