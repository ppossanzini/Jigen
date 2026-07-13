const local: App.I18n.Schema = {
  system: {
    title: 'Jigen Insight',
    updateTitle: 'System Version Update Notification',
    updateContent: 'A new version of the system has been detected. Do you want to refresh the page immediately?',
    updateConfirm: 'Refresh immediately',
    updateCancel: 'Later',
    copyright: 'Copyright © {year} Jigen'
  },
  common: {
    action: 'Action',
    add: 'Add',
    addSuccess: 'Add Success',
    backToHome: 'Back to home',
    batchDelete: 'Batch Delete',
    cancel: 'Cancel',
    close: 'Close',
    check: 'Check',
    selectAll: 'Select All',
    expandColumn: 'Expand Column',
    columnSetting: 'Column Setting',
    config: 'Config',
    confirm: 'Confirm',
    delete: 'Delete',
    deleteSuccess: 'Delete Success',
    confirmDelete: 'Are you sure you want to delete?',
    edit: 'Edit',
    warning: 'Warning',
    error: 'Error',
    index: 'Index',
    keywordSearch: 'Please enter keyword',
    logout: 'Logout',
    logoutConfirm: 'Are you sure you want to log out?',
    lookForward: 'Coming soon',
    modify: 'Modify',
    modifySuccess: 'Modify Success',
    noData: 'No Data',
    operate: 'Operate',
    pleaseCheckValue: 'Please check whether the value is valid',
    refresh: 'Refresh',
    reset: 'Reset',
    search: 'Search',
    switch: 'Switch',
    tip: 'Tip',
    trigger: 'Trigger',
    update: 'Update',
    updateSuccess: 'Update Success',
    userCenter: 'User Center',
    yesOrNo: {
      yes: 'Yes',
      no: 'No'
    }
  },
  request: {
    serverUnreachable: 'Cannot reach the Jigen server. Check that it is running and the API endpoint is correct.',
    sessionExpired: 'Your session has expired, please log in again'
  },
  theme: {
    themeDrawerTitle: 'Theme Configuration',
    tabs: {
      appearance: 'Appearance',
      layout: 'Layout',
      general: 'General',
      preset: 'Preset'
    },
    appearance: {
      themeSchema: {
        title: 'Theme Schema',
        light: 'Light',
        dark: 'Dark',
        auto: 'Follow System'
      },
      grayscale: 'Grayscale',
      colourWeakness: 'Colour Weakness',
      themeColor: {
        title: 'Theme Color',
        primary: 'Primary',
        info: 'Info',
        success: 'Success',
        warning: 'Warning',
        error: 'Error',
        followPrimary: 'Follow Primary'
      },
      themeRadius: {
        title: 'Theme Radius'
      },
      recommendColor: 'Apply Recommended Color Algorithm',
      recommendColorDesc: 'The recommended color algorithm refers to',
      preset: {
        title: 'Theme Presets',
        apply: 'Apply',
        applySuccess: 'Preset applied successfully',
        default: {
          name: 'Jigen',
          desc: 'The default Jigen Insight theme — lime primary, dark by default'
        }
      }
    },
    layout: {
      layoutMode: {
        title: 'Layout Mode',
        vertical: 'Vertical Mode',
        horizontal: 'Horizontal Mode',
        'vertical-mix': 'Vertical Mix Mode',
        'vertical-hybrid-header-first': 'Left Hybrid Header-First',
        'top-hybrid-sidebar-first': 'Top-Hybrid Sidebar-First',
        'top-hybrid-header-first': 'Top-Hybrid Header-First',
        vertical_detail: 'Vertical menu layout, with the menu on the left and content on the right.',
        'vertical-mix_detail':
          'Vertical mix-menu layout, with the primary menu on the dark left side and the secondary menu on the lighter left side.',
        'vertical-hybrid-header-first_detail':
          'Left hybrid layout, with the primary menu at the top, the secondary menu on the dark left side, and the tertiary menu on the lighter left side.',
        horizontal_detail: 'Horizontal menu layout, with the menu at the top and content below.',
        'top-hybrid-sidebar-first_detail':
          'Top hybrid layout, with the primary menu on the left and the secondary menu at the top.',
        'top-hybrid-header-first_detail':
          'Top hybrid layout, with the primary menu at the top and the secondary menu on the left.'
      },
      tab: {
        title: 'Tab Settings',
        visible: 'Tab Visible',
        cache: 'Tag Bar Info Cache',
        cacheTip: 'Keep the tab bar information after leaving the page',
        height: 'Tab Height',
        mode: {
          title: 'Tab Mode',
          slider: 'Slider',
          chrome: 'Chrome',
          button: 'Button'
        },
        closeByMiddleClick: 'Close Tab by Middle Click',
        closeByMiddleClickTip: 'Enable closing tabs by clicking with the middle mouse button'
      },
      header: {
        title: 'Header Settings',
        height: 'Header Height',
        breadcrumb: {
          visible: 'Breadcrumb Visible',
          showIcon: 'Breadcrumb Icon Visible'
        }
      },
      sider: {
        title: 'Sider Settings',
        inverted: 'Dark Sider',
        width: 'Sider Width',
        collapsedWidth: 'Sider Collapsed Width',
        mixWidth: 'Mix Sider Width',
        mixCollapsedWidth: 'Mix Sider Collapse Width',
        mixChildMenuWidth: 'Mix Child Menu Width',
        autoSelectFirstMenu: 'Auto Select First Submenu',
        autoSelectFirstMenuTip:
          'When a first-level menu is clicked, the first submenu is automatically selected and navigated to the deepest level'
      },
      footer: {
        title: 'Footer Settings',
        visible: 'Footer Visible',
        fixed: 'Fixed Footer',
        height: 'Footer Height',
        right: 'Right Footer'
      },
      content: {
        title: 'Content Area Settings',
        scrollMode: {
          title: 'Scroll Mode',
          tip: 'The theme scroll only scrolls the main part, the outer scroll can carry the header and footer together',
          wrapper: 'Wrapper',
          content: 'Content'
        },
        page: {
          animate: 'Page Animate',
          mode: {
            title: 'Page Animate Mode',
            fade: 'Fade',
            'fade-slide': 'Slide',
            'fade-bottom': 'Fade Zoom',
            'fade-scale': 'Fade Scale',
            'zoom-fade': 'Zoom Fade',
            'zoom-out': 'Zoom Out',
            none: 'None'
          }
        },
        fixedHeaderAndTab: 'Fixed Header And Tab'
      }
    },
    general: {
      title: 'General Settings',
      watermark: {
        title: 'Watermark Settings',
        visible: 'Watermark Full Screen Visible',
        text: 'Custom Watermark Text',
        enableUserName: 'Enable User Name Watermark',
        enableTime: 'Show Current Time',
        timeFormat: 'Time Format'
      },
      multilingual: {
        title: 'Multilingual Settings',
        visible: 'Display multilingual button'
      },
      globalSearch: {
        title: 'Global Search Settings',
        visible: 'Display GlobalSearch button'
      }
    },
    configOperation: {
      copyConfig: 'Copy Config',
      copySuccessMsg: 'Copy Success, Please replace the variable "themeSettings" in "src/theme/settings.ts"',
      resetConfig: 'Reset Config',
      resetSuccessMsg: 'Reset Success'
    }
  },
  route: {
    login: 'Login',
    403: 'No Permission',
    404: 'Page Not Found',
    500: 'Server Error',
    'iframe-page': 'Iframe',
    'auth-callback': 'Signing in',
    overview: 'Overview',
    databases: 'Databases',
    collections: 'Collections',
    workbench: 'Workbench',
    'graph-explorer': 'Graph Explorer',
    security: 'Security',
    settings: 'Settings'
  },
  page: {
    login: {
      common: {
        loginOrRegister: 'Log in',
        userNamePlaceholder: 'Please enter user name',
        passwordPlaceholder: 'Please enter password',
        loginSuccess: 'Login successfully',
        welcomeBack: 'Welcome back, {userName} !',
        invalidCredentials: 'Invalid user name or password'
      },
      pwdLogin: {
        title: 'Sign in'
      },
      callback: {
        title: 'Signing in',
        signingIn: 'Completing sign-in, please wait...',
        error: 'Sign-in could not be completed. Please try again.',
        backToLogin: 'Back to login'
      }
    },
    overview: {
      title: 'Overview',
      desc: 'Server health dashboard: CPU, memory, database and collection counts, ingestion queue.',
      kpi: {
        cpu: 'CPU',
        memory: 'Memory',
        databases: 'Databases',
        collections: 'Collections',
        vectors: 'Vectors',
        ingestionQueue: 'Ingestion queue'
      },
      charts: {
        cpu: 'CPU usage',
        memory: 'Memory usage',
        ingestionQueue: 'Ingestion queue length',
        databaseSizes: 'Database sizes',
        content: 'content',
        vector: 'vectors',
        index: 'index'
      },
      window: {
        title: 'Window',
        '1m': '1 min',
        '5m': '5 min',
        '10m': '10 min',
        '1h': '1 hour'
      },
      state: {
        loadFailed: 'Failed to load server metrics',
        connectionLost: 'Connection to the server lost — showing the last received data'
      }
    },
    databases: {
      title: 'Databases',
      desc: 'Browse databases, storage breakdown and assigned users.',
      table: {
        name: 'Name',
        created: 'Created',
        collections: 'Collections',
        vectors: 'Vectors',
        contentSize: 'Content size',
        vectorSize: 'Vector size',
        indexSize: 'Index size',
        freeSpace: 'Free space',
        users: 'Users'
      },
      actions: {
        create: 'Create database',
        delete: 'Delete'
      },
      create: {
        title: 'Create database',
        nameLabel: 'Database name',
        namePlaceholder: 'e.g. production, staging-eu',
        nameInvalid: 'Only letters, digits, dot, dash and underscore are allowed (1-64 characters)',
        submit: 'Create',
        success: 'Database created'
      },
      delete: {
        title: 'Delete database',
        warning: 'This permanently removes "{name}" and cannot be undone.',
        deleteFilesLabel: 'Also delete database files on disk',
        deleteFilesHint: 'If unchecked, the database is detached but its files remain on disk.',
        success: 'Database deleted'
      },
      detail: {
        title: 'Database details',
        storageBreakdown: 'Storage breakdown per collection',
        collectionsSummary: 'Collections',
        usersTitle: 'Assigned users',
        usersPlaceholder: 'Select users with access to this database',
        usersSave: 'Save users',
        usersSaveSuccess: 'Users updated',
        noCollections: 'This database has no collections yet'
      },
      empty: {
        noDatabases: 'No databases yet'
      }
    },
    collections: {
      title: 'Collections',
      desc: 'Browse collections and their HNSW index metrics.',
      databaseSelector: {
        label: 'Database',
        placeholder: 'Select a database'
      },
      table: {
        name: 'Name',
        vectors: 'Vectors',
        dimensions: 'Dimensions',
        contentSize: 'Content size',
        vectorSize: 'Vector size',
        indexSize: 'Index size',
        maxLevel: 'Max level',
        averageDegree: 'Avg. degree',
        deletedCount: 'Deleted',
        quantization: 'Quantization'
      },
      detail: {
        title: 'Collection details',
        indexTitle: 'Index metrics',
        noIndex: 'This collection has no vector index metrics yet',
        openInWorkbench: 'Open in Workbench',
        openInGraphExplorer: 'Open in Graph Explorer'
      },
      empty: {
        noDatabaseSelected: 'Select a database to see its collections',
        noCollections: 'This database has no collections yet',
        noDatabases: 'No databases yet — create one on the Databases page'
      }
    },
    workbench: {
      title: 'Workbench',
      desc: 'Search collections and inspect or edit documents.',
      query: {
        database: 'Database',
        databasePlaceholder: 'Select a database',
        collections: 'Collections',
        collectionsPlaceholder: 'Select one or more collections',
        mode: {
          sentence: 'Sentence',
          embeddings: 'Embeddings'
        },
        sentencePlaceholder: 'Enter a sentence to embed and search',
        embeddingsPlaceholder: 'Comma-separated numbers, e.g. 0.12, -0.5, 0.87',
        embeddingsInvalid: 'Enter a comma-separated list of numbers',
        top: 'Top K',
        search: 'Search',
        collectionsRequired: 'Select at least one collection',
        inputRequired: 'Enter a sentence or an embeddings vector'
      },
      timing: {
        title: 'Timing',
        embedding: 'Embedding',
        search: 'Search',
        merge: 'Merge',
        sort: 'Sort',
        perCollection: 'Per-collection search time'
      },
      results: {
        title: 'Results',
        score: 'Score',
        key: 'Key',
        collection: 'Collection',
        content: 'Content',
        empty: 'No results yet — run a search above',
        selectPrompt: 'Select a database and at least one collection, then search'
      },
      detail: {
        title: 'Result',
        keyType: 'Key type'
      },
      document: {
        title: 'Document operations',
        collection: 'Collection',
        collectionPlaceholder: 'Select a collection',
        keyLabel: 'Key',
        keyPlaceholder: 'Document key',
        keyTypeLabel: 'Key type',
        keyTypeAuto: 'Auto',
        get: 'Get',
        upsert: 'Upsert',
        delete: 'Delete',
        sentenceLabel: 'Sentence (optional, computes the embedding server-side)',
        sentencePlaceholder: 'Sentence to embed for this document',
        jsonLabel: 'Content (JSON)',
        jsonPlaceholder: 'JSON object, e.g. "field": "value"',
        jsonInvalid: 'Invalid JSON',
        getSuccess: 'Document loaded',
        upsertSuccess: 'Document saved',
        deleteSuccess: 'Document deleted',
        notFound: 'No document found for this key'
      }
    },
    'graph-explorer': {
      title: 'Graph Explorer',
      desc: 'Visualize the HNSW index graph in 2D or 3D.',
      controls: {
        database: 'Database',
        databasePlaceholder: 'Select a database',
        collection: 'Collection',
        collectionPlaceholder: 'Select a collection',
        dimensions: 'View',
        dimensions2d: '2D',
        dimensions3d: '3D',
        limit: 'Node limit',
        level: 'Level filter',
        levelPlaceholder: 'All levels',
        levelClear: 'Clear',
        load: 'Load graph'
      },
      stats: {
        total: 'Total nodes',
        live: 'Live nodes',
        deleted: 'Deleted nodes',
        returned: 'Returned nodes',
        maxLevel: 'Max level',
        entrypoint: 'Entrypoint',
        truncated: 'Truncated'
      },
      chart: {
        entrypointLabel: 'Entrypoint',
        tooltipKey: 'Key',
        tooltipPosition: 'Position ID',
        tooltipLevel: 'Max level',
        tooltipDegree: 'Degree',
        tooltipDeleted: 'Deleted',
        tooltipEdgeLevel: 'Edge level',
        legendLevel: 'Level {level}',
        legendDeleted: 'Deleted'
      },
      empty: {
        noDatabaseSelected: 'Select a database to explore its collections',
        noCollectionSelected: 'Select a collection to load its index graph',
        noNodes: 'This collection has no indexed vectors yet — the graph is empty'
      }
    },
    security: {
      title: 'Security',
      desc: 'Manage users, roles and applications.',
      tabs: {
        users: 'Users',
        roles: 'Roles',
        apps: 'Apps'
      },
      users: {
        table: {
          userName: 'User name',
          id: 'Id'
        },
        actions: {
          create: 'Create user',
          delete: 'Delete'
        },
        create: {
          title: 'Create user',
          userNameLabel: 'User name',
          userNamePlaceholder: 'e.g. jane.doe',
          passwordLabel: 'Password',
          passwordPlaceholder: 'Initial password',
          rolesLabel: 'Roles',
          rolesPlaceholder: 'Assign roles (optional)',
          submit: 'Create',
          success: 'User created'
        },
        delete: {
          warning: 'This permanently deletes user "{name}" and cannot be undone.',
          success: 'User deleted'
        },
        detail: {
          title: 'User details',
          idLabel: 'Id',
          userNameLabel: 'User name',
          passwordLabel: 'New password',
          passwordPlaceholder: 'Leave blank to keep the current password',
          rolesLabel: 'Roles',
          rolesPlaceholder: 'Select roles',
          permissionsLabel: 'Permissions',
          noPermissions: 'No permissions granted',
          save: 'Save changes',
          saveSuccess: 'User updated'
        },
        empty: 'No users yet'
      },
      roles: {
        table: {
          name: 'Name',
          id: 'Id'
        },
        actions: {
          create: 'Create role',
          delete: 'Delete'
        },
        create: {
          title: 'Create role',
          nameLabel: 'Role name',
          namePlaceholder: 'e.g. SecurityAdmin',
          submit: 'Create',
          success: 'Role created'
        },
        delete: {
          warning: 'This permanently deletes role "{name}" and cannot be undone.',
          success: 'Role deleted'
        },
        detail: {
          title: 'Role details',
          nameLabel: 'Role name',
          save: 'Save changes',
          saveSuccess: 'Role updated',
          usersTitle: 'Assigned users',
          noUsers: 'No users assigned to this role'
        },
        empty: 'No roles yet'
      },
      apps: {
        desc: 'Registered OAuth2 / OpenID Connect clients (read-only).',
        table: {
          clientId: 'Client Id',
          displayName: 'Display name'
        },
        empty: 'No registered applications'
      }
    },
    settings: {
      title: 'Settings',
      desc: 'Theme, API endpoint and locale preferences.'
    }
  },
  form: {
    required: 'Cannot be empty',
    userName: {
      required: 'Please enter user name',
      invalid: 'User name format is incorrect'
    },
    phone: {
      required: 'Please enter phone number',
      invalid: 'Phone number format is incorrect'
    },
    pwd: {
      required: 'Please enter password',
      invalid: '6-18 characters, including letters, numbers, and underscores'
    },
    confirmPwd: {
      required: 'Please enter password again',
      invalid: 'The two passwords are inconsistent'
    },
    code: {
      required: 'Please enter verification code',
      invalid: 'Verification code format is incorrect'
    },
    email: {
      required: 'Please enter email',
      invalid: 'Email format is incorrect'
    }
  },
  dropdown: {
    closeCurrent: 'Close Current',
    closeOther: 'Close Other',
    closeLeft: 'Close Left',
    closeRight: 'Close Right',
    closeAll: 'Close All',
    pin: 'Pin Tab',
    unpin: 'Unpin Tab'
  },
  icon: {
    themeConfig: 'Theme Configuration',
    themeSchema: 'Theme Schema',
    lang: 'Switch Language',
    fullscreen: 'Fullscreen',
    fullscreenExit: 'Exit Fullscreen',
    reload: 'Reload Page',
    collapse: 'Collapse Menu',
    expand: 'Expand Menu',
    pin: 'Pin',
    unpin: 'Unpin'
  },
  datatable: {
    itemCount: 'Total {total} items',
    fixed: {
      left: 'Left Fixed',
      right: 'Right Fixed',
      unFixed: 'Unfixed'
    }
  }
};

export default local;
