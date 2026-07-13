<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { fetchListRoles, fetchUpdateRole, fetchUsersInRole } from '@/service/api';
import type { RoleSummary, UserSummary } from '@/service/api-types';
import { useAppStore } from '@/store/modules/app';
import { $t } from '@/locales';

defineOptions({
  name: 'RoleDetailDrawer'
});

const visible = defineModel<boolean>('visible', { default: false });

const appStore = useAppStore();
// drawer width in viewport units (rule 3.5): full-width sheet on mobile, ~2/5 of the viewport otherwise
const drawerWidth = computed(() => (appStore.isMobile ? '92vw' : '38vw'));

interface Props {
  roleId: string;
}

const props = defineProps<Props>();

interface Emits {
  (e: 'updated'): void;
}

const emit = defineEmits<Emits>();

const loading = ref(false);
const role = ref<RoleSummary | null>(null);
const nameModel = ref('');
const saving = ref(false);

const usersLoading = ref(false);
const users = ref<UserSummary[]>([]);

const userColumns = [
  { title: () => $t('page.security.users.table.userName'), key: 'userName' },
  { title: () => $t('page.security.users.table.id'), key: 'id' }
];

async function loadRole() {
  if (!props.roleId) return;

  loading.value = true;
  role.value = null;

  // no single-role-by-id endpoint: find it in the already-fetched list
  const { data, error } = await fetchListRoles();
  loading.value = false;

  if (!error) {
    const found = (data ?? []).find(r => r.id === props.roleId) ?? null;
    role.value = found;
    nameModel.value = found?.name ?? '';
  }
}

async function loadUsers() {
  if (!props.roleId) return;

  usersLoading.value = true;
  const { data, error } = await fetchUsersInRole(props.roleId);
  usersLoading.value = false;

  if (!error) {
    users.value = data ?? [];
  }
}

watch(visible, value => {
  if (value) {
    loadRole();
    loadUsers();
  }
});

async function handleSave() {
  if (!props.roleId) return;

  saving.value = true;
  const { error } = await fetchUpdateRole(props.roleId, { name: nameModel.value });
  saving.value = false;

  if (!error) {
    window.$message?.success($t('page.security.roles.detail.saveSuccess'));
    if (role.value) role.value = { ...role.value, name: nameModel.value };
    emit('updated');
  }
}
</script>

<template>
  <NDrawer v-model:show="visible" :width="drawerWidth">
    <NDrawerContent :title="$t('page.security.roles.detail.title')" closable>
      <NSkeleton v-if="loading" text :repeat="6" />
      <NEmpty v-else-if="!role" :description="$t('common.noData')" />
      <div v-else class="flex-col gap-24px">
        <NForm label-placement="top">
          <NFormItem :label="$t('page.security.roles.detail.nameLabel')">
            <NInput v-model:value="nameModel" />
          </NFormItem>
        </NForm>

        <div class="flex justify-end">
          <NButton type="primary" :loading="saving" @click="handleSave">
            {{ $t('page.security.roles.detail.save') }}
          </NButton>
        </div>

        <div class="flex-col gap-8px">
          <span class="text-14px font-600">{{ $t('page.security.roles.detail.usersTitle') }}</span>
          <NEmpty v-if="!usersLoading && !users.length" :description="$t('page.security.roles.detail.noUsers')" />
          <NDataTable
            v-else
            :columns="userColumns"
            :data="users"
            :loading="usersLoading"
            :bordered="false"
            size="small"
            :max-height="240"
            :pagination="false"
            virtual-scroll
            :row-key="(row: UserSummary) => row.id ?? ''"
          />
        </div>
      </div>
    </NDrawerContent>
  </NDrawer>
</template>
