<script setup lang="ts">
import { computed, onActivated, onMounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { fetchListCollections, fetchSearchCollections } from '@/service/api';
import type { SearchCollectionsResult } from '@/service/api-types';
import type { DocumentKeyType } from '@/service/api-types';
import { useAppStore } from '@/store/modules/app';
import { useDatabaseStore } from '@/store/modules/database';
import { useGraphHighlightStore } from '@/store/modules/graph-highlight';
import { useRouterPush } from '@/hooks/common/router';
import { decodeKey } from '@/utils/key-codec';
import { $t } from '@/locales';
import TimingStrip from './modules/timing-strip.vue';
import ResultsTable from './modules/results-table.vue';
import type { ResultRow } from './modules/results-table.vue';
import ResultDetailDrawer from './modules/result-detail-drawer.vue';
import DocumentPanel from './modules/document-panel.vue';

// Name matches the route name so <KeepAlive :include="routeStore.cacheRoutes"> (see
// global-content/index.vue) can find and cache this page — see routes.ts's `keepAlive: true`.
defineOptions({
  name: 'workbench'
});

const appStore = useAppStore();
const databaseStore = useDatabaseStore();
const graphHighlightStore = useGraphHighlightStore();
const { routerPushByKey } = useRouterPush();
const route = useRoute();

const drawerWidth = computed(() => (appStore.isMobile ? '92vw' : '38vw'));

const databaseOptions = computed(() => databaseStore.databases.map(name => ({ label: name, value: name })));

const collectionOptions = ref<string[]>([]);
const selectedCollections = ref<string[]>([]);

async function loadCollections() {
  selectedCollections.value = [];
  collectionOptions.value = [];

  if (!databaseStore.current) return;

  const { data, error } = await fetchListCollections(databaseStore.current);
  if (!error) {
    collectionOptions.value = data ?? [];
  }
}

watch(() => databaseStore.current, loadCollections);

const mode = ref<'sentence' | 'embeddings'>('sentence');
const sentenceInput = ref('');
const embeddingsInput = ref('');
const topK = ref(10);
const validationError = ref('');

const searching = ref(false);
const searchError = ref('');
const result = ref<SearchCollectionsResult | null>(null);

function parseEmbeddings(): number[] | null {
  const parts = embeddingsInput.value
    .split(',')
    .map(p => p.trim())
    .filter(Boolean)
    .map(Number);

  if (!parts.length || parts.some(Number.isNaN)) return null;

  return parts;
}

async function handleSearch() {
  validationError.value = '';
  searchError.value = '';

  if (!selectedCollections.value.length) {
    validationError.value = $t('page.workbench.query.collectionsRequired');
    return;
  }

  const sentence = mode.value === 'sentence' ? sentenceInput.value.trim() : '';
  const embeddings = mode.value === 'embeddings' ? parseEmbeddings() : null;

  if (mode.value === 'sentence' && !sentence) {
    validationError.value = $t('page.workbench.query.inputRequired');
    return;
  }

  if (mode.value === 'embeddings' && !embeddings) {
    validationError.value = $t('page.workbench.query.embeddingsInvalid');
    return;
  }

  searching.value = true;

  const { data, error } = await fetchSearchCollections(databaseStore.current, {
    collections: selectedCollections.value,
    sentence: sentence || undefined,
    embeddings: embeddings ?? undefined,
    top: topK.value
  });

  searching.value = false;

  if (error) {
    searchError.value = error.response?.data && typeof error.response.data === 'string' ? error.response.data : $t('request.serverUnreachable');
    result.value = null;
    return;
  }

  result.value = data;
}

// flatten per-collection results (kept collection-attributed, unlike the server's anonymous
// `mergedResults`) and sort by score so the table reads like a single merged/ranked result set
const rows = computed<ResultRow[]>(() => {
  const groups = result.value?.collectionsResults ?? [];

  const flat = groups.flatMap(group =>
    (group.results ?? []).map((item, index) => ({
      id: `${group.collection}:${item.key}:${index}`,
      collection: group.collection ?? '',
      key: item.key ?? '',
      score: Number(item.score ?? 0),
      content: item.content
    }))
  );

  return flat.sort((a, b) => b.score - a.score);
});

// --- "Visualizza nel grafo": hand the current search off to Graph Explorer ---
const collectionsWithResults = computed(() => (result.value?.collectionsResults ?? []).filter(group => (group.results?.length ?? 0) > 0));

const graphMenuOptions = computed(() =>
  collectionsWithResults.value.map(group => ({ label: group.collection ?? '', key: group.collection ?? '' }))
);

function viewInGraph(collectionName: string) {
  const group = collectionsWithResults.value.find(g => g.collection === collectionName);
  if (!group || !databaseStore.current) return;

  const matches = new Map<string, number>();
  for (const item of group.results ?? []) {
    if (item.key) matches.set(item.key, Number(item.score ?? 0));
  }

  graphHighlightStore.stage({
    database: databaseStore.current,
    collection: collectionName,
    matches
  });

  routerPushByKey('graph-explorer', { query: { db: databaseStore.current, collection: collectionName } });
}

// --- result detail drawer ---
const detailVisible = ref(false);
const detailRow = ref<ResultRow | null>(null);

// --- document panel prefill from a selected result row ---
const docDrawerVisible = ref(false);
const docPresetCollection = ref('');
const docPresetKey = ref('');
const docPresetKeyType = ref<DocumentKeyType | undefined>(undefined);

function openDetail(row: ResultRow) {
  detailRow.value = row;
  detailVisible.value = true;

  // also stage the row in the document drawer, so a click can flow straight into edit/delete
  docPresetCollection.value = row.collection;
  docPresetKey.value = decodeKey(row.key);
  docPresetKeyType.value = undefined;
}

// One-time bootstrap: this page is kept alive across navigation (see routes.ts), so onMounted
// fires only once for its whole lifetime — populate the collections dropdown here if a database
// is already selected globally. Deep-link query params are handled separately in onActivated
// below, which (unlike onMounted) fires on every re-entry.
onMounted(async () => {
  if (!databaseStore.loaded) {
    await databaseStore.loadDatabases();
  }

  if (databaseStore.current) {
    await loadCollections();
  }
});

// Runs on every visit to this page, including the first (right after onMounted — see Vue's
// KeepAlive docs). With no db/collection in the query string this is a no-op, so navigating away
// and back (e.g. via the sidebar) leaves the selected database/collections, search input and
// results exactly as the user left them.
onActivated(async () => {
  const queryDb = route.query.db;
  if (typeof queryDb !== 'string' || !databaseStore.databases.includes(queryDb)) return;

  if (databaseStore.current !== queryDb) {
    databaseStore.setCurrent(queryDb);
  }

  await loadCollections();

  const queryCollection = route.query.collection;
  if (typeof queryCollection === 'string' && collectionOptions.value.includes(queryCollection)) {
    selectedCollections.value = [queryCollection];
  }
});
</script>

<template>
  <div class="h-full flex-col gap-16px">
    <NCard :bordered="false" size="small" class="card-wrapper">
      <div class="flex-col gap-12px">
        <div class="grid grid-cols-1 gap-12px md:grid-cols-2 xl:grid-cols-5">
          <NFormItem :label="$t('page.workbench.query.database')" :show-feedback="false">
            <NSelect
              :value="databaseStore.current || null"
              :options="databaseOptions"
              :placeholder="$t('page.workbench.query.databasePlaceholder')"
              @update:value="(value: string) => databaseStore.setCurrent(value)"
            />
          </NFormItem>
          <NFormItem class="xl:col-span-2" :label="$t('page.workbench.query.collections')" :show-feedback="false">
            <NSelect
              v-model:value="selectedCollections"
              multiple
              filterable
              :options="collectionOptions.map(name => ({ label: name, value: name }))"
              :placeholder="$t('page.workbench.query.collectionsPlaceholder')"
            />
          </NFormItem>
          <NFormItem :label="$t('page.workbench.query.top')" :show-feedback="false">
            <NInputNumber v-model:value="topK" :min="1" :max="1000" class="w-full" />
          </NFormItem>
          <NFormItem :show-feedback="false" label=" ">
            <NRadioGroup v-model:value="mode">
              <NRadioButton value="sentence">{{ $t('page.workbench.query.mode.sentence') }}</NRadioButton>
              <NRadioButton value="embeddings">{{ $t('page.workbench.query.mode.embeddings') }}</NRadioButton>
            </NRadioGroup>
          </NFormItem>
        </div>

        <div class="flex items-start gap-12px">
          <NInput
            v-if="mode === 'sentence'"
            v-model:value="sentenceInput"
            type="textarea"
            :autosize="{ minRows: 1, maxRows: 3 }"
            :placeholder="$t('page.workbench.query.sentencePlaceholder')"
            class="flex-1"
            @keyup.enter.exact="handleSearch"
          />
          <NInput
            v-else
            v-model:value="embeddingsInput"
            type="textarea"
            :autosize="{ minRows: 1, maxRows: 3 }"
            :placeholder="$t('page.workbench.query.embeddingsPlaceholder')"
            class="flex-1"
          />

          <NButton type="primary" :loading="searching" :disabled="!databaseStore.current" @click="handleSearch">
            <template #icon><SvgIcon icon="mdi:magnify" /></template>
            {{ $t('page.workbench.query.search') }}
          </NButton>
        </div>

        <NAlert v-if="validationError" type="warning" :show-icon="true" :bordered="false" closable @close="validationError = ''">
          {{ validationError }}
        </NAlert>
      </div>
    </NCard>

    <NCard v-if="result" :bordered="false" size="small" class="card-wrapper">
      <TimingStrip :result="result" />
    </NCard>

    <NCard
      :bordered="false"
      size="small"
      :title="$t('page.workbench.results.title')"
      class="card-wrapper min-h-0 flex-1"
      content-class="h-full min-h-0 flex-col"
    >
      <template #header-extra>
        <div class="flex-y-center gap-8px">
          <NButton
            v-if="collectionsWithResults.length === 1"
            size="small"
            quaternary
            @click="viewInGraph(collectionsWithResults[0].collection ?? '')"
          >
            <template #icon><SvgIcon icon="mdi:graph-outline" /></template>
            {{ $t('page.workbench.results.viewInGraph') }}
          </NButton>
          <NDropdown v-else-if="collectionsWithResults.length > 1" trigger="click" :options="graphMenuOptions" @select="viewInGraph">
            <NButton size="small" quaternary>
              <template #icon><SvgIcon icon="mdi:graph-outline" /></template>
              {{ $t('page.workbench.results.viewInGraph') }}
            </NButton>
          </NDropdown>
          <NButton size="small" quaternary @click="docDrawerVisible = true">
            <template #icon><SvgIcon icon="mdi:file-document-edit-outline" /></template>
            {{ $t('page.workbench.document.title') }}
          </NButton>
        </div>
      </template>
      <NResult
        v-if="searchError"
        status="error"
        :title="$t('page.overview.state.loadFailed')"
        :description="searchError"
        class="flex-1 flex-center"
      />
      <NEmpty
        v-else-if="!result"
        :description="$t('page.workbench.results.selectPrompt')"
        class="flex-1 flex-center"
      />
      <NEmpty v-else-if="!rows.length" :description="$t('page.workbench.results.empty')" class="flex-1 flex-center" />
      <ResultsTable v-else :rows="rows" :loading="searching" @select="openDetail" />
    </NCard>

    <NDrawer v-model:show="docDrawerVisible" :width="drawerWidth">
      <NDrawerContent :title="$t('page.workbench.document.title')" closable>
        <DocumentPanel
          :database="databaseStore.current"
          :collection-options="collectionOptions"
          :preset-collection="docPresetCollection"
          :preset-key="docPresetKey"
          :preset-key-type="docPresetKeyType"
        />
      </NDrawerContent>
    </NDrawer>

    <ResultDetailDrawer v-model:visible="detailVisible" :row="detailRow" />
  </div>
</template>
