<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { fetchGetDocumentJson } from '@/service/api';
import { useAppStore } from '@/store/modules/app';
import { decodeKey } from '@/utils/key-codec';
import { $t } from '@/locales';
import type { PreparedNode } from './graph-data';

defineOptions({
  name: 'NodeDetailDrawer'
});

const visible = defineModel<boolean>('visible', { default: false });

interface Props {
  database: string;
  collection: string;
  node: PreparedNode | null;
}

const props = defineProps<Props>();

const appStore = useAppStore();
const drawerWidth = computed(() => (appStore.isMobile ? '92vw' : '38vw'));

const loading = ref(false);
const error = ref('');
const content = ref<unknown>(undefined);

const decodedKey = computed(() => (props.node?.key ? decodeKey(props.node.key) : ''));

const prettyContent = computed(() => {
  if (content.value === undefined || content.value === null) return '';

  try {
    return JSON.stringify(content.value, null, 2);
  } catch {
    return String(content.value);
  }
});

async function loadDocument() {
  const node = props.node;

  error.value = '';
  content.value = undefined;

  if (!node) return;

  if (!node.key) {
    error.value = $t('page.graph-explorer.detail.noKey');
    return;
  }

  loading.value = true;
  const { data, error: fetchError } = await fetchGetDocumentJson(props.database, props.collection, decodeKey(node.key));
  loading.value = false;

  if (fetchError) {
    error.value =
      fetchError.response?.data && typeof fetchError.response.data === 'string'
        ? fetchError.response.data
        : $t('page.graph-explorer.detail.loadFailed');
    return;
  }

  if (data) {
    content.value = data.content;
    error.value = '';
  } else {
    error.value = $t('page.graph-explorer.detail.notFound');
  }
}

watch([() => props.node, visible], () => {
  if (visible.value && props.node) {
    loadDocument();
  }
});
</script>

<template>
  <NDrawer v-model:show="visible" :width="drawerWidth">
    <NDrawerContent :title="$t('page.graph-explorer.detail.title')" closable>
      <NSkeleton v-if="loading" text :repeat="6" />
      <NEmpty v-else-if="error" :description="error" class="flex-1 flex-center" />
      <div v-else-if="node" class="flex-col gap-16px">
        <NDescriptions :column="1" label-placement="left" size="small">
          <NDescriptionsItem :label="$t('page.graph-explorer.detail.position')">#{{ node.positionId }}</NDescriptionsItem>
          <NDescriptionsItem :label="$t('page.graph-explorer.detail.key')">{{ decodedKey || '—' }}</NDescriptionsItem>
          <NDescriptionsItem :label="$t('page.graph-explorer.detail.level')">{{ node.maxLevel }}</NDescriptionsItem>
          <NDescriptionsItem :label="$t('page.graph-explorer.detail.degree')">{{ node.degree }}</NDescriptionsItem>
          <NDescriptionsItem v-if="node.isEntrypoint" :label="$t('page.graph-explorer.detail.entrypoint')">
            {{ $t('page.graph-explorer.detail.entrypoint') }}
          </NDescriptionsItem>
          <NDescriptionsItem v-if="node.isDeleted" :label="$t('page.graph-explorer.detail.deleted')">
            {{ $t('page.graph-explorer.detail.deleted') }}
          </NDescriptionsItem>
        </NDescriptions>
        <div class="flex-col gap-8px">
          <span class="text-14px font-600">{{ $t('page.graph-explorer.detail.content') }}</span>
          <NCode :code="prettyContent" language="json" word-wrap show-line-numbers />
        </div>
      </div>
    </NDrawerContent>
  </NDrawer>
</template>
