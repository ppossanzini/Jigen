const en = {
  app: {
    brand: 'Jigen Insight',
    subtitle: 'Data intelligence workspace',
    shell: {
      console: 'Console Mode',
      workspace: 'Operational data workspace',
      liveOps: 'live monitoring',
      connected: 'Connected',
      readOnly: 'Safe mode',
    },
    menu: {
      dashboard: 'Dashboard',
      collections: 'Collections',
      security: 'Security',
    },
    actions: {
      logout: 'Sign out',
      openFeature: 'Open feature',
      enter: 'Enter',
      backHome: 'Back to dashboard',
    },
  },
  auth: {
    title: 'Sign in to the workspace',
    tagline: 'Connect to the Jigen backend securely.',
    sideTitle: 'Jigen operational console',
    sideDescription: 'A dense and readable environment for data monitoring, quick actions, and runtime status.',
    sideItemA: 'Technical panel-based navigation',
    sideItemB: 'Fast access to in-progress features',
    sideItemC: 'Immediate feedback on status and session',
    username: 'Username',
    password: 'Password',
    required: 'Fill in both fields.',
    loginError: 'Login failed. Check credentials.',
  },
  main: {
    title: 'Welcome to Jigen Insight',
    description: 'Explore data, monitor system status, and prepare upcoming modules.',
    consoleMode: 'High density view',
    endpoint: 'Active OpenAPI endpoint',
    metrics: {
      connections: 'Connections',
      workspaces: 'Workspaces',
      alerts: 'Alerts',
      latency: 'Latency',
    },
    panels: {
      overview: 'Runtime overview',
      stable: 'Stable',
      quickActions: 'Quick actions',
      next: 'next steps',
      activity: 'Recent activity',
      realtime: 'realtime',
    },
    actions: {
      collections: 'Open collections module',
      users: 'Open users module',
      docs: 'Open docs area',
    },
    timeline: {
      boot: 'Console initialized successfully',
      auth: 'User session validated',
      scan: 'Endpoint scan completed',
      ready: 'Dashboard ready for new features',
    },
    spotlightTitle: 'Quick start',
    spotlightDescription: 'From here, move directly to the next feature in progress.',
  },
  comingSoon: {
    title: 'Feature coming soon',
    message: 'The {feature} section is under construction. Check back soon.',
  },
  usersRoles: {
    title: 'Users and roles management',
    description: 'Manage application users, permissions, and operational roles from the console.',
    common: {
      columns: {
        actions: 'Actions',
      },
    },
    actions: {
      createUser: 'New user',
      createRole: 'New role',
      edit: 'Edit',
      delete: 'Delete',
      save: 'Save',
      cancel: 'Cancel',
    },
    users: {
      title: 'Users',
      columns: {
        userName: 'Username',
        password: 'Password',
        roles: 'Roles',
      },
      dialog: {
        createTitle: 'Create user',
        editTitle: 'Edit user',
      },
    },
    roles: {
      title: 'Roles',
      columns: {
        name: 'Role name',
      },
      dialog: {
        createTitle: 'Create role',
        editTitle: 'Edit role',
      },
    },
    feedback: {
      warningTitle: 'Warning',
      genericError: 'Operation failed.',
      userCreated: 'User created successfully.',
      userUpdated: 'User updated successfully.',
      userDeleted: 'User deleted successfully.',
      roleCreated: 'Role created successfully.',
      roleUpdated: 'Role updated successfully.',
      roleDeleted: 'Role deleted successfully.',
      confirmDeleteUser: 'Confirm user deletion?',
      confirmDeleteRole: 'Confirm role deletion?',
    },
  },
  security: {
    layout: {
      title: 'Security Management',
      description: 'Manage users, roles, and application permissions.',
    },
    common: {
      edit: 'Edit',
      delete: 'Delete',
      save: 'Save',
      cancel: 'Cancel',
      manage: 'Manage',
      status: 'Status',
      warning: 'Warning',
      error: 'Operation failed',
    },
    users: {
      title: 'Users',
      columns: {
        userName: 'Username',
        password: 'Password',
        roles: 'Roles',
      },
      details: {
        title: 'User details',
      },
      userRoles: {
        title: 'User roles',
        manage: 'Manage roles',
      },
      actions: {
        create: 'New user',
      },
      dialog: {
        createTitle: 'Create new user',
        editTitle: 'Edit user',
      },
      validation: {
        usernameRequired: 'Username is required',
        passwordRequired: 'Password is required',
      },
      feedback: {
        created: 'User created successfully.',
        updated: 'User updated successfully.',
        deleted: 'User deleted successfully.',
        rolesUpdated: 'User roles updated successfully.',
        confirmDelete: 'Confirm user deletion?',
      },
    },
    roles: {
      title: 'Roles',
      columns: {
        name: 'Role name',
      },
      roleUsers: {
        title: 'Users with this role',
      },
      actions: {
        create: 'New role',
      },
      dialog: {
        createTitle: 'Create new role',
        editTitle: 'Edit role',
      },
      validation: {
        nameRequired: 'Role name is required',
      },
      feedback: {
        created: 'Role created successfully.',
        updated: 'Role updated successfully.',
        deleted: 'Role deleted successfully.',
        confirmDelete: 'Confirm role deletion?',
      },
    },
  },
} as const

export default en
