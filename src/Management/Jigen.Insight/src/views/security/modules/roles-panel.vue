<script setup lang="ts">
import { h, onMounted, ref } from 'vue';
import { NButton } from 'naive-ui';
import type { DataTableColumns } from 'naive-ui';
import { fetchDeleteRole, fetchListRoles } from '@/service/api';
import type { RoleSummary } from '@/service/api-types';
import { $t } from '@/locales';
import CreateRoleModal from './create-role-modal.vue';
import RoleDetailDrawer from './role-detail-drawer.vue';

defineOptions({
  name: 'RolesPanel'
});

const rows = ref<RoleSummary[]>([]);
const loading = ref(false);
const hasError = ref(false);
const settled = ref(false);

async function loadRows() {
  loading.value = true;
  hasError.value = false;

  const { data, error } = await fetchListRoles();

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
function openDelete(role: RoleSummary) {
  window.$dialog?.warning({
    title: $t('common.tip'),
    content: $t('page.security.roles.delete.warning', { name: role.name ?? '' }),
    positiveText: $t('common.confirm'),
    negativeText: $t('common.cancel'),
    onPositiveClick: async () => {
      const { error } = await fetchDeleteRole(role.id ?? '');
      if (!error) {
        window.$message?.success($t('page.security.roles.delete.success'));
        if (detailId.value === role.id) {
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

function openDetail(role: RoleSummary) {
  detailId.value = role.id ?? '';
  detailVisible.value = true;
}

function handleUpdated() {
  loadRows();
}

const columns: DataTableColumns<RoleSummary> = [
  {
    title: () => $t('page.security.roles.table.name'),
    key: 'name',
    render: row =>
      h(
        'a',
        {
          class: 'text-primary cursor-pointer hover:underline',
          onClick: () => openDetail(row)
        },
        row.name ?? ''
      )
  },
  {
    title: () => $t('page.security.roles.table.id'),
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
        () => $t('page.security.roles.actions.delete')
      )
  }
];
</script>

<template>
  <div class="h-full flex-col gap-12px">
    <div class="flex justify-end">
      <NButton type="primary" @click="createVisible = true">
        <template #icon><SvgIcon icon="mdi:plus" /></template>
        {{ $t('page.security.roles.actions.create') }}
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
      :description="$t('page.security.roles.empty')"
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
      :row-key="(row: RoleSummary) => row.id ?? ''"
    />

    <CreateRoleModal v-model:visible="createVisible" @created="handleCreated" />
    <RoleDetailDrawer v-model:visible="detailVisible" :role-id="detailId" @updated="handleUpdated" />
  </div>
</template>
