<script setup lang="ts">
import { h, onMounted, ref } from 'vue';
import { NButton } from 'naive-ui';
import type { DataTableColumns } from 'naive-ui';
import { fetchDatabaseDetails, fetchListDatabases } from '@/service/api';
import type { DatabaseDetails, DatabaseUserInfo } from '@/service/api-types';
import { useDatabaseStore } from '@/store/modules/database';
import { formatBytes, formatCount, toNum } from '@/utils/format';
import { $t } from '@/locales';
import CreateDatabaseModal from './modules/create-database-modal.vue';
import DeleteDatabaseDialog from './modules/delete-database-dialog.vue';
import DatabaseDetailDrawer from './modules/database-detail-drawer.vue';

defineOptions({
  name: 'DatabasesPage'
});

const databaseStore = useDatabaseStore();

const rows = ref<DatabaseDetails[]>([]);
const loading = ref(false);
const hasError = ref(false);
const settled = ref(false);

async function loadRows() {
  loading.value = true;
  hasError.value = false;

  const { data: names, error } = await fetchListDatabases();

  if (error) {
    hasError.value = true;
    loading.value = false;
    settled.value = true;
    return;
  }

  const details = await Promise.all(
    (names ?? []).map(async name => {
      const { data, error: detailError } = await fetchDatabaseDetails(name);
      return detailError ? { name } : (data as DatabaseDetails);
    })
  );

  rows.value = details;
  loading.value = false;
  settled.value = true;
}

onMounted(loadRows);

// --- create ---
const createVisible = ref(false);

function handleCreated() {
  loadRows();
}

// --- delete ---
const deleteVisible = ref(false);
const deleteTarget = ref('');

function openDelete(name: string) {
  deleteTarget.value = name;
  deleteVisible.value = true;
}

function handleDeleted(name: string) {
  if (databaseStore.current === name) {
    databaseStore.setCurrent('');
  }
  loadRows();
}

// --- detail drawer ---
const detailVisible = ref(false);
const detailLoading = ref(false);
const detailData = ref<DatabaseDetails | null>(null);

async function openDetail(name: string) {
  detailVisible.value = true;
  detailLoading.value = true;
  detailData.value = null;
  databaseStore.setCurrent(name);

  const { data, error } = await fetchDatabaseDetails(name);
  detailLoading.value = false;

  if (!error) {
    detailData.value = data;
  }
}

function handleUsersUpdated(users: DatabaseUserInfo[]) {
  if (detailData.value) {
    detailData.value = { ...detailData.value, users };
  }
  const name = detailData.value?.name;
  const row = rows.value.find(r => r.name === name);
  if (row) row.users = users;
}

const columns: DataTableColumns<DatabaseDetails> = [
  {
    title: () => $t('page.databases.table.name'),
    key: 'name',
    render: row =>
      h(
        'a',
        {
          class: 'text-primary cursor-pointer hover:underline',
          onClick: () => openDetail(row.name ?? '')
        },
        row.name ?? ''
      )
  },
  {
    title: () => $t('page.databases.table.created'),
    key: 'createdAtUtc',
    render: row => (row.createdAtUtc ? new Date(row.createdAtUtc).toLocaleString() : '—')
  },
  {
    title: () => $t('page.databases.table.collections'),
    key: 'collectionsCount',
    render: row => formatCount(row.collectionsCount)
  },
  {
    title: () => $t('page.databases.table.vectors'),
    key: 'vectors',
    render: row => formatCount(row.vectors)
  },
  {
    title: () => $t('page.databases.table.contentSize'),
    key: 'contentSize',
    render: row => formatBytes(row.contentSize)
  },
  {
    title: () => $t('page.databases.table.vectorSize'),
    key: 'vectorSize',
    render: row => formatBytes(row.vectorSize)
  },
  {
    title: () => $t('page.databases.table.indexSize'),
    key: 'indexSize',
    render: row => formatBytes(row.indexSize)
  },
  {
    title: () => $t('page.databases.table.freeSpace'),
    key: 'freeSpace',
    render: row => formatBytes(toNum(row.contentFreeSpace) + toNum(row.vectorFreeSpace))
  },
  {
    title: () => $t('page.databases.table.users'),
    key: 'usersCount',
    render: row => formatCount(row.usersCount)
  },
  {
    title: () => $t('common.action'),
    key: 'actions',
    render: row =>
      h(
        NButton,
        {
          size: 'small',
          type: 'error',
          quaternary: true,
          onClick: () => openDelete(row.name ?? '')
        },
        () => $t('page.databases.actions.delete')
      )
  }
];
</script>

<template>
  <div class="h-full flex-col gap-16px">
    <NCard :bordered="false" size="small" class="card-wrapper">
      <div class="flex-y-center justify-between gap-12px">
        <span class="text-16px font-600">{{ $t('page.databases.title') }}</span>
        <NButton type="primary" @click="createVisible = true">
          <template #icon><SvgIcon icon="mdi:plus" /></template>
          {{ $t('page.databases.actions.create') }}
        </NButton>
      </div>
    </NCard>

    <NCard :bordered="false" size="small" class="card-wrapper min-h-0 flex-1" content-class="h-full min-h-0 flex-col">
      <NResult
        v-if="!loading && hasError"
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
        :description="$t('page.databases.empty.noDatabases')"
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
        :row-key="(row: DatabaseDetails) => row.name ?? ''"
      />
    </NCard>

    <CreateDatabaseModal v-model:visible="createVisible" @created="handleCreated" />
    <DeleteDatabaseDialog v-model:visible="deleteVisible" :name="deleteTarget" @deleted="handleDeleted" />
    <DatabaseDetailDrawer
      v-model:visible="detailVisible"
      :details="detailData"
      :loading="detailLoading"
      @users-updated="handleUsersUpdated"
    />
  </div>
</template>
