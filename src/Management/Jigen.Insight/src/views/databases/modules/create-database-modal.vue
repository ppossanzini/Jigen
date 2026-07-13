<script setup lang="ts">
import { reactive, ref } from 'vue';
import { fetchCreateDatabase } from '@/service/api';
import { useNaiveForm } from '@/hooks/common/form';
import { $t } from '@/locales';

defineOptions({
  name: 'CreateDatabaseModal'
});

const visible = defineModel<boolean>('visible', { default: false });

interface Emits {
  (e: 'created', name: string): void;
}

const emit = defineEmits<Emits>();

const { formRef, validate, restoreValidation } = useNaiveForm();
const submitting = ref(false);

const model = reactive({ name: '' });

// database names become filesystem folder/file names on the server, so keep them filesystem-safe
const NAME_PATTERN = /^[\w.-]{1,64}$/;

const rules = {
  name: [
    { required: true, message: $t('form.required'), trigger: ['input', 'blur'] },
    { pattern: NAME_PATTERN, message: $t('page.databases.create.nameInvalid'), trigger: ['input', 'blur'] }
  ]
};

function handleClose() {
  visible.value = false;
  model.name = '';
  restoreValidation();
}

async function handleSubmit() {
  await validate();

  submitting.value = true;
  const { error } = await fetchCreateDatabase(model.name);
  submitting.value = false;

  if (!error) {
    window.$message?.success($t('page.databases.create.success'));
    emit('created', model.name);
    handleClose();
  }
}
</script>

<template>
  <NModal
    v-model:show="visible"
    :title="$t('page.databases.create.title')"
    preset="card"
    class="w-90vw max-w-420px"
    :bordered="false"
    @after-leave="handleClose"
  >
    <NForm ref="formRef" :model="model" :rules="rules" @keyup.enter="handleSubmit">
      <NFormItem path="name" :label="$t('page.databases.create.nameLabel')">
        <NInput v-model:value="model.name" :placeholder="$t('page.databases.create.namePlaceholder')" />
      </NFormItem>
    </NForm>
    <template #footer>
      <div class="flex-y-center justify-end gap-12px">
        <NButton @click="visible = false">{{ $t('common.cancel') }}</NButton>
        <NButton type="primary" :loading="submitting" @click="handleSubmit">
          {{ $t('page.databases.create.submit') }}
        </NButton>
      </div>
    </template>
  </NModal>
</template>
