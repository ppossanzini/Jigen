<script setup lang="ts">
import { onMounted, ref } from 'vue';
import type { DataTableColumns } from 'naive-ui';
import { fetchListApps } from '@/service/api';
import type { AppSummary } from '@/service/api-types';
import { $t } from '@/locales';

defineOptions({
  name: 'AppsPanel'
});

const rows = ref<AppSummary[]>([]);
const loading = ref(false);
const hasError = ref(false);
const settled = ref(false);

async function loadRows() {
  loading.value = true;
  hasError.value = false;

  const { data, error } = await fetchListApps();

  loading.value = false;
  settled.value = true;

  if (error) {
    hasError.value = true;
    return;
  }

  rows.value = data ?? [];
}

onMounted(loadRows);

const columns: DataTableColumns<AppSummary> = [
  { title: () => $t('page.security.apps.table.clientId'), key: 'clientId' },
  { title: () => $t('page.security.apps.table.displayName'), key: 'displayName' }
];
</script>

<template>
  <div class="h-full flex-col gap-12px">
    <span class="text-12px text-gray-500">{{ $t('page.security.apps.desc') }}</span>

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
      :description="$t('page.security.apps.empty')"
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
      :row-key="(row: AppSummary) => row.clientId ?? ''"
    />
  </div>
</template>
