<script setup lang="ts">
import { reactive, ref } from 'vue';
import { fetchCreateRole } from '@/service/api';
import { useNaiveForm } from '@/hooks/common/form';
import { $t } from '@/locales';

defineOptions({
  name: 'CreateRoleModal'
});

const visible = defineModel<boolean>('visible', { default: false });

interface Emits {
  (e: 'created'): void;
}

const emit = defineEmits<Emits>();

const { formRef, validate, restoreValidation } = useNaiveForm();
const submitting = ref(false);

const model = reactive({ name: '' });

const rules = {
  name: [{ required: true, message: $t('form.required'), trigger: ['input', 'blur'] }]
};

function handleClose() {
  visible.value = false;
  model.name = '';
  restoreValidation();
}

async function handleSubmit() {
  await validate();

  submitting.value = true;
  const { error } = await fetchCreateRole({ name: model.name });
  submitting.value = false;

  if (!error) {
    window.$message?.success($t('page.security.roles.create.success'));
    emit('created');
    handleClose();
  }
}
</script>

<template>
  <NModal
    v-model:show="visible"
    :title="$t('page.security.roles.create.title')"
    preset="card"
    class="w-90vw max-w-420px"
    :bordered="false"
    @after-leave="handleClose"
  >
    <NForm ref="formRef" :model="model" :rules="rules" @keyup.enter="handleSubmit">
      <NFormItem path="name" :label="$t('page.security.roles.create.nameLabel')">
        <NInput v-model:value="model.name" :placeholder="$t('page.security.roles.create.namePlaceholder')" />
      </NFormItem>
    </NForm>
    <template #footer>
      <div class="flex-y-center justify-end gap-12px">
        <NButton @click="visible = false">{{ $t('common.cancel') }}</NButton>
        <NButton type="primary" :loading="submitting" @click="handleSubmit">
          {{ $t('page.security.roles.create.submit') }}
        </NButton>
      </div>
    </template>
  </NModal>
</template>
