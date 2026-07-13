<script setup lang="ts">
import { ref, watch } from 'vue';
import { fetchDeleteDatabase } from '@/service/api';
import { $t } from '@/locales';

defineOptions({
  name: 'DeleteDatabaseDialog'
});

const visible = defineModel<boolean>('visible', { default: false });

interface Props {
  name: string;
}

const props = defineProps<Props>();

interface Emits {
  (e: 'deleted', name: string): void;
}

const emit = defineEmits<Emits>();

const deleteFiles = ref(true);
const submitting = ref(false);

watch(visible, value => {
  if (value) deleteFiles.value = true;
});

async function handleConfirm() {
  submitting.value = true;
  const { error } = await fetchDeleteDatabase(props.name, deleteFiles.value);
  submitting.value = false;

  if (!error) {
    window.$message?.success($t('page.databases.delete.success'));
    emit('deleted', props.name);
    visible.value = false;
  }
}
</script>

<template>
  <NModal
    v-model:show="visible"
    :title="$t('page.databases.delete.title')"
    preset="card"
    class="w-90vw max-w-420px"
    :bordered="false"
  >
    <NSpace vertical :size="16">
      <NAlert type="error" :show-icon="true" :bordered="false">
        {{ $t('page.databases.delete.warning', { name }) }}
      </NAlert>
      <NCheckbox v-model:checked="deleteFiles">
        {{ $t('page.databases.delete.deleteFilesLabel') }}
      </NCheckbox>
      <p class="text-12px text-gray-500">{{ $t('page.databases.delete.deleteFilesHint') }}</p>
    </NSpace>
    <template #footer>
      <div class="flex-y-center justify-end gap-12px">
        <NButton @click="visible = false">{{ $t('common.cancel') }}</NButton>
        <NButton type="error" :loading="submitting" @click="handleConfirm">
          {{ $t('page.databases.actions.delete') }}
        </NButton>
      </div>
    </template>
  </NModal>
</template>
