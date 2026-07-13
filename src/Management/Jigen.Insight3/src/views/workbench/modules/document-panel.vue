<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { fetchDeleteDocument, fetchGetDocumentJson, fetchSetDocument } from '@/service/api';
import type { DocumentKeyType } from '@/service/api-types';
import { $t } from '@/locales';

defineOptions({
  name: 'DocumentPanel'
});

interface Props {
  database: string;
  collectionOptions: string[];
  /** Prefilled from a selected search result row, if any */
  presetCollection?: string;
  presetKey?: string;
  presetKeyType?: DocumentKeyType;
}

const props = defineProps<Props>();

/** Empty string stands for "auto-detect" (n-select options can't carry a `null` value) */
const AUTO_KEY_TYPE = '';

const collection = ref('');
const key = ref('');
const keyType = ref<DocumentKeyType | ''>(AUTO_KEY_TYPE);
const sentence = ref('');
const jsonText = ref('');
const jsonError = ref('');
const loading = ref(false);

watch(
  () => [props.presetCollection, props.presetKey, props.presetKeyType],
  () => {
    if (props.presetCollection) collection.value = props.presetCollection;
    if (props.presetKey) key.value = props.presetKey;
    keyType.value = props.presetKeyType ?? AUTO_KEY_TYPE;
  }
);

const keyTypeOptions = computed<{ label: string; value: DocumentKeyType | typeof AUTO_KEY_TYPE }[]>(() => [
  { label: $t('page.workbench.document.keyTypeAuto'), value: AUTO_KEY_TYPE },
  { label: 'string', value: 'string' },
  { label: 'int', value: 'int' },
  { label: 'long', value: 'long' },
  { label: 'guid', value: 'guid' }
]);

const collectionSelectOptions = computed(() => props.collectionOptions.map(name => ({ label: name, value: name })));

function canAct() {
  return Boolean(props.database && collection.value && key.value);
}

async function handleGet() {
  if (!canAct()) return;

  loading.value = true;
  const { data, error } = await fetchGetDocumentJson(props.database, collection.value, key.value, keyType.value || undefined);
  loading.value = false;

  if (!error) {
    jsonText.value = data?.content !== undefined ? JSON.stringify(data.content, null, 2) : '';
    jsonError.value = '';

    if (data) {
      window.$message?.success($t('page.workbench.document.getSuccess'));
    } else {
      window.$message?.warning($t('page.workbench.document.notFound'));
    }
  }
}

function parsePayload(): unknown | undefined {
  if (!jsonText.value.trim()) return undefined;

  try {
    const parsed = JSON.parse(jsonText.value);
    jsonError.value = '';
    return parsed;
  } catch {
    jsonError.value = $t('page.workbench.document.jsonInvalid');
    return undefined;
  }
}

async function handleUpsert() {
  if (!canAct()) return;

  const hasJson = jsonText.value.trim().length > 0;
  let payload: unknown;

  if (hasJson) {
    payload = parsePayload();
    if (jsonError.value) return;
  }

  loading.value = true;
  const { error } = await fetchSetDocument(
    props.database,
    collection.value,
    key.value,
    { payload, sentence: sentence.value || undefined },
    keyType.value || undefined
  );
  loading.value = false;

  if (!error) {
    window.$message?.success($t('page.workbench.document.upsertSuccess'));
  }
}

async function handleDelete() {
  if (!canAct()) return;

  loading.value = true;
  const { error } = await fetchDeleteDocument(props.database, collection.value, key.value, keyType.value || undefined);
  loading.value = false;

  if (!error) {
    window.$message?.success($t('page.workbench.document.deleteSuccess'));
    jsonText.value = '';
  }
}
</script>

<template>
  <div class="flex-col gap-12px">
    <div class="grid grid-cols-1 gap-12px sm:grid-cols-2">
      <NFormItem :label="$t('page.workbench.document.collection')" :show-feedback="false">
        <NSelect
          v-model:value="collection"
          :options="collectionSelectOptions"
          :placeholder="$t('page.workbench.document.collectionPlaceholder')"
        />
      </NFormItem>
      <NFormItem :label="$t('page.workbench.document.keyTypeLabel')" :show-feedback="false">
        <NSelect v-model:value="keyType" :options="keyTypeOptions" />
      </NFormItem>
    </div>
    <NFormItem :label="$t('page.workbench.document.keyLabel')" :show-feedback="false">
      <NInput v-model:value="key" :placeholder="$t('page.workbench.document.keyPlaceholder')" />
    </NFormItem>
    <NFormItem :label="$t('page.workbench.document.sentenceLabel')" :show-feedback="false">
      <NInput
        v-model:value="sentence"
        type="textarea"
        :autosize="{ minRows: 1, maxRows: 3 }"
        :placeholder="$t('page.workbench.document.sentencePlaceholder')"
      />
    </NFormItem>
    <NFormItem
      :label="$t('page.workbench.document.jsonLabel')"
      :feedback="jsonError"
      :validation-status="jsonError ? 'error' : undefined"
    >
      <NInput
        v-model:value="jsonText"
        type="textarea"
        :autosize="{ minRows: 3, maxRows: 8 }"
        :placeholder="$t('page.workbench.document.jsonPlaceholder')"
      />
    </NFormItem>
    <div class="flex flex-wrap gap-8px">
      <NButton :disabled="!canAct()" :loading="loading" @click="handleGet">
        {{ $t('page.workbench.document.get') }}
      </NButton>
      <NButton type="primary" :disabled="!canAct()" :loading="loading" @click="handleUpsert">
        {{ $t('page.workbench.document.upsert') }}
      </NButton>
      <NButton type="error" :disabled="!canAct()" :loading="loading" @click="handleDelete">
        {{ $t('page.workbench.document.delete') }}
      </NButton>
    </div>
  </div>
</template>
