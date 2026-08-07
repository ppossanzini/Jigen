<script setup lang="ts">
import type { DataTableColumns } from 'naive-ui';
import { toNum } from '@/utils/format';
import { decodeKey } from '@/utils/key-codec';
import { $t } from '@/locales';
import { useResizableColumns } from '@/hooks/common/resizable-columns';

defineOptions({
  name: 'ResultsTable'
});

export interface ResultRow {
  id: string;
  collection: string;
  key: string;
  score: number;
  content: unknown;
}

interface Props {
  rows: ResultRow[];
  loading: boolean;
}

defineProps<Props>();

interface Emits {
  (e: 'select', row: ResultRow): void;
}

const emit = defineEmits<Emits>();

function contentPreview(content: unknown): string {
  if (content === null || content === undefined) return '—';

  const text = typeof content === 'string' ? content : JSON.stringify(content);

  return text.length > 140 ? `${text.slice(0, 140)}…` : text;
}

const rawColumns: DataTableColumns<ResultRow> = [
  {
    title: () => $t('page.workbench.results.score'),
    key: 'score',
    width: 100,
    render: row => toNum(row.score).toFixed(4)
  },
  {
    title: () => $t('page.workbench.results.key'),
    key: 'key',
    width: 180,
    ellipsis: { tooltip: true },
    render: row => decodeKey(row.key)
  },
  {
    title: () => $t('page.workbench.results.collection'),
    key: 'collection',
    width: 160
  },
  {
    title: () => $t('page.workbench.results.content'),
    key: 'content',
    width: 320,
    ellipsis: { tooltip: true },
    render: row => contentPreview(row.content)
  }
];

const { columns } = useResizableColumns(rawColumns, {
  storageKey: 'workbench-results',
  defaultWidth: 150
});
</script>

<template>
  <NDataTable
    :columns="columns"
    :data="rows"
    :loading="loading"
    :bordered="false"
    :pagination="false"
    flex-height
    virtual-scroll
    class="h-full"
    :row-key="(row: ResultRow) => row.id"
    :row-props="(row: ResultRow) => ({ class: 'cursor-pointer', onClick: () => emit('select', row) })"
  />
</template>
