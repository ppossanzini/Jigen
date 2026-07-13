<script setup lang="ts">
import { reactive, ref, watch } from 'vue';
import { fetchCreateUser, fetchListRoles } from '@/service/api';
import { useNaiveForm } from '@/hooks/common/form';
import { $t } from '@/locales';

defineOptions({
  name: 'CreateUserModal'
});

const visible = defineModel<boolean>('visible', { default: false });

interface Emits {
  (e: 'created'): void;
}

const emit = defineEmits<Emits>();

const { formRef, validate, restoreValidation } = useNaiveForm();
const submitting = ref(false);

const model = reactive({
  userName: '',
  password: '',
  roles: [] as string[]
});

const rules = {
  userName: [{ required: true, message: $t('form.required'), trigger: ['input', 'blur'] }],
  password: [{ required: true, message: $t('form.required'), trigger: ['input', 'blur'] }]
};

const roleOptions = ref<{ label: string; value: string }[]>([]);
const rolesLoading = ref(false);

async function loadRoles() {
  rolesLoading.value = true;
  const { data, error } = await fetchListRoles();
  rolesLoading.value = false;

  if (!error) {
    roleOptions.value = (data ?? []).map(role => ({ label: role.name ?? '', value: role.name ?? '' }));
  }
}

watch(visible, value => {
  if (value) loadRoles();
});

function handleClose() {
  visible.value = false;
  model.userName = '';
  model.password = '';
  model.roles = [];
  restoreValidation();
}

async function handleSubmit() {
  await validate();

  submitting.value = true;
  const { error } = await fetchCreateUser({
    userName: model.userName,
    password: model.password,
    roles: model.roles
  });
  submitting.value = false;

  if (!error) {
    window.$message?.success($t('page.security.users.create.success'));
    emit('created');
    handleClose();
  }
}
</script>

<template>
  <NModal
    v-model:show="visible"
    :title="$t('page.security.users.create.title')"
    preset="card"
    class="w-90vw max-w-420px"
    :bordered="false"
    @after-leave="handleClose"
  >
    <NForm ref="formRef" :model="model" :rules="rules" @keyup.enter="handleSubmit">
      <NFormItem path="userName" :label="$t('page.security.users.create.userNameLabel')">
        <NInput v-model:value="model.userName" :placeholder="$t('page.security.users.create.userNamePlaceholder')" />
      </NFormItem>
      <NFormItem path="password" :label="$t('page.security.users.create.passwordLabel')">
        <NInput
          v-model:value="model.password"
          type="password"
          show-password-on="click"
          :placeholder="$t('page.security.users.create.passwordPlaceholder')"
        />
      </NFormItem>
      <NFormItem :label="$t('page.security.users.create.rolesLabel')">
        <NSelect
          v-model:value="model.roles"
          multiple
          filterable
          :loading="rolesLoading"
          :options="roleOptions"
          :placeholder="$t('page.security.users.create.rolesPlaceholder')"
        />
      </NFormItem>
    </NForm>
    <template #footer>
      <div class="flex-y-center justify-end gap-12px">
        <NButton @click="visible = false">{{ $t('common.cancel') }}</NButton>
        <NButton type="primary" :loading="submitting" @click="handleSubmit">
          {{ $t('page.security.users.create.submit') }}
        </NButton>
      </div>
    </template>
  </NModal>
</template>
