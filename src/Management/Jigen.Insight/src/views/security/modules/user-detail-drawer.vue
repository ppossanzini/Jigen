<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { fetchListRoles, fetchUpdateUser, fetchUserDetail } from '@/service/api';
import type { UserDetail } from '@/service/api-types';
import { useAppStore } from '@/store/modules/app';
import { $t } from '@/locales';

defineOptions({
  name: 'UserDetailDrawer'
});

const visible = defineModel<boolean>('visible', { default: false });

const appStore = useAppStore();
// drawer width in viewport units (rule 3.5): full-width sheet on mobile, ~2/5 of the viewport otherwise
const drawerWidth = computed(() => (appStore.isMobile ? '92vw' : '38vw'));

interface Props {
  userId: string;
}

const props = defineProps<Props>();

interface Emits {
  (e: 'updated'): void;
}

const emit = defineEmits<Emits>();

const loading = ref(false);
const detail = ref<UserDetail | null>(null);

const userNameModel = ref('');
const passwordModel = ref('');
const rolesModel = ref<string[]>([]);

const roleOptions = ref<{ label: string; value: string }[]>([]);
const rolesLoading = ref(false);
const saving = ref(false);

async function loadRoles() {
  rolesLoading.value = true;
  const { data, error } = await fetchListRoles();
  rolesLoading.value = false;

  if (!error) {
    roleOptions.value = (data ?? []).map(role => ({ label: role.name ?? '', value: role.name ?? '' }));
  }
}

async function loadDetail() {
  if (!props.userId) return;

  loading.value = true;
  detail.value = null;

  const { data, error } = await fetchUserDetail(props.userId);
  loading.value = false;

  if (!error && data) {
    detail.value = data;
    userNameModel.value = data.userName ?? '';
    passwordModel.value = '';
    rolesModel.value = [...(data.roles ?? [])];
  }
}

watch(visible, value => {
  if (value) {
    loadRoles();
    loadDetail();
  }
});

async function handleSave() {
  if (!props.userId) return;

  saving.value = true;
  const { data, error } = await fetchUpdateUser(props.userId, {
    userName: userNameModel.value,
    password: passwordModel.value || undefined,
    roles: rolesModel.value
  });
  saving.value = false;

  if (!error) {
    window.$message?.success($t('page.security.users.detail.saveSuccess'));
    passwordModel.value = '';
    if (data) {
      detail.value = data;
      rolesModel.value = [...(data.roles ?? [])];
    }
    emit('updated');
  }
}
</script>

<template>
  <NDrawer v-model:show="visible" :width="drawerWidth">
    <NDrawerContent :title="$t('page.security.users.detail.title')" closable>
      <NSkeleton v-if="loading" text :repeat="6" />
      <NEmpty v-else-if="!detail" :description="$t('common.noData')" />
      <div v-else class="flex-col gap-24px">
        <NDescriptions :column="1" label-placement="left" size="small">
          <NDescriptionsItem :label="$t('page.security.users.detail.idLabel')">{{ detail.id }}</NDescriptionsItem>
        </NDescriptions>

        <NForm label-placement="top">
          <NFormItem :label="$t('page.security.users.detail.userNameLabel')">
            <NInput v-model:value="userNameModel" />
          </NFormItem>
          <NFormItem :label="$t('page.security.users.detail.passwordLabel')">
            <NInput
              v-model:value="passwordModel"
              type="password"
              show-password-on="click"
              :placeholder="$t('page.security.users.detail.passwordPlaceholder')"
            />
          </NFormItem>
          <NFormItem :label="$t('page.security.users.detail.rolesLabel')">
            <NSelect
              v-model:value="rolesModel"
              multiple
              filterable
              :loading="rolesLoading"
              :options="roleOptions"
              :placeholder="$t('page.security.users.detail.rolesPlaceholder')"
            />
          </NFormItem>
        </NForm>

        <div class="flex-col gap-8px">
          <span class="text-14px font-600">{{ $t('page.security.users.detail.permissionsLabel') }}</span>
          <NEmpty v-if="!detail.permissions?.length" :description="$t('page.security.users.detail.noPermissions')" />
          <NSpace v-else :size="8">
            <NTag v-for="permission in detail.permissions" :key="permission" size="small">{{ permission }}</NTag>
          </NSpace>
        </div>

        <div class="flex justify-end">
          <NButton type="primary" :loading="saving" @click="handleSave">
            {{ $t('page.security.users.detail.save') }}
          </NButton>
        </div>
      </div>
    </NDrawerContent>
  </NDrawer>
</template>
