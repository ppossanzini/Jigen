<script setup lang="ts">
import { h, onMounted, ref } from 'vue';
import { NButton } from 'naive-ui';
import type { DataTableColumns } from 'naive-ui';
import { fetchDeleteUser, fetchListUsers } from '@/service/api';
import type { UserSummary } from '@/service/api-types';
import { $t } from '@/locales';
import CreateUserModal from './create-user-modal.vue';
import UserDetailDrawer from './user-detail-drawer.vue';

defineOptions({
  name: 'UsersPanel'
});

const rows = ref<UserSummary[]>([]);
const loading = ref(false);
const hasError = ref(false);
const settled = ref(false);

async function loadRows() {
  loading.value = true;
  hasError.value = false;

  const { data, error } = await fetchListUsers();

  loading.value = false;
  settled.value = true;

  if (error) {
    hasError.value = true;
    return;
  }

  rows.value = data ?? [];
}

onMounted(loadRows);

// --- create ---
const createVisible = ref(false);

function handleCreated() {
  loadRows();
}

// --- delete ---
function openDelete(user: UserSummary) {
  window.$dialog?.warning({
    title: $t('common.tip'),
    content: $t('page.security.users.delete.warning', { name: user.userName ?? '' }),
    positiveText: $t('common.confirm'),
    negativeText: $t('common.cancel'),
    onPositiveClick: async () => {
      const { error } = await fetchDeleteUser(user.id ?? '');
      if (!error) {
        window.$message?.success($t('page.security.users.delete.success'));
        if (detailId.value === user.id) {
          detailVisible.value = false;
        }
        loadRows();
      }
    }
  });
}

// --- detail drawer ---
const detailVisible = ref(false);
const detailId = ref('');

function openDetail(user: UserSummary) {
  detailId.value = user.id ?? '';
  detailVisible.value = true;
}

function handleUpdated() {
  loadRows();
}

const columns: DataTableColumns<UserSummary> = [
  {
    title: () => $t('page.security.users.table.userName'),
    key: 'userName',
    render: row =>
      h(
        'a',
        {
          class: 'text-primary dark:text-primary-800 cursor-pointer hover:underline',
          onClick: () => openDetail(row)
        },
        row.userName ?? ''
      )
  },
  {
    title: () => $t('page.security.users.table.id'),
    key: 'id',
    render: row => h('span', { class: 'text-gray-500' }, row.id ?? '')
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
          onClick: () => openDelete(row)
        },
        () => $t('page.security.users.actions.delete')
      )
  }
];
</script>

<template>
  <div class="h-full flex-col gap-12px">
    <div class="flex justify-end">
      <NButton type="primary" @click="createVisible = true">
        <template #icon><SvgIcon icon="mdi:plus" /></template>
        {{ $t('page.security.users.actions.create') }}
      </NButton>
    </div>

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
      :description="$t('page.security.users.empty')"
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
      class="h-full min-h-0 flex-1"
      :row-key="(row: UserSummary) => row.id ?? ''"
    />

    <CreateUserModal v-model:visible="createVisible" @created="handleCreated" />
    <UserDetailDrawer v-model:visible="detailVisible" :user-id="detailId" @updated="handleUpdated" />
  </div>
</template>
