const it = {
  app: {
    brand: 'Jigen Insight',
    subtitle: 'Data intelligence workspace',
    shell: {
      console: 'Console Mode',
      workspace: 'Operational data workspace',
      liveOps: 'monitoraggio live',
      connected: 'Connesso',
      readOnly: 'Safe mode',
    },
    menu: {
      dashboard: 'Dashboard',
      collections: 'Collezioni',
      security: 'Security',
    },
    actions: {
      logout: 'Esci',
      openFeature: 'Apri funzionalita',
      enter: 'Entra',
      backHome: 'Torna alla dashboard',
    },
  },
  auth: {
    title: 'Accedi al workspace',
    tagline: 'Connettiti al backend Jigen in modo sicuro.',
    sideTitle: 'Console operativa Jigen',
    sideDescription: 'Un ambiente denso e leggibile per monitorare dati, azioni rapide e stato runtime.',
    sideItemA: 'Navigazione tecnica a pannelli',
    sideItemB: 'Accesso rapido alle feature in sviluppo',
    sideItemC: 'Feedback immediato su stato e sessione',
    username: 'Username',
    password: 'Password',
    required: 'Compila entrambi i campi.',
    loginError: 'Accesso non riuscito. Verifica le credenziali.',
  },
  main: {
    title: 'Benvenuto in Jigen Insight',
    description: 'Esplora i dati, controlla lo stato del sistema e prepara i moduli operativi.',
    consoleMode: 'High density view',
    endpoint: 'Endpoint OpenAPI attivo',
    metrics: {
      connections: 'Connessioni',
      workspaces: 'Workspace',
      alerts: 'Alert',
      latency: 'Latenza',
    },
    panels: {
      overview: 'Overview runtime',
      stable: 'Stabile',
      quickActions: 'Quick actions',
      next: 'prossimi step',
      activity: 'Recent activity',
      realtime: 'realtime',
    },
    actions: {
      collections: 'Apri modulo collezioni',
      users: 'Apri modulo utenti',
      docs: 'Apri area docs',
    },
    timeline: {
      boot: 'Console inizializzata correttamente',
      auth: 'Sessione utente verificata',
      scan: 'Analisi endpoint completata',
      ready: 'Dashboard pronta per nuove feature',
    },
    spotlightTitle: 'Avvio rapido',
    spotlightDescription: 'Da qui puoi entrare subito nella prossima funzionalita in sviluppo.',
  },
  comingSoon: {
    title: 'Funzionalita in arrivo',
    message: 'La sezione {feature} e in costruzione. Torna presto.',
  },
  usersRoles: {
    title: 'Gestione utenti e ruoli',
    description: 'Amministra utenti applicativi, permessi e ruoli operativi dalla console.',
    common: {
      columns: {
        actions: 'Azioni',
      },
    },
    actions: {
      createUser: 'Nuovo utente',
      createRole: 'Nuovo ruolo',
      edit: 'Modifica',
      delete: 'Elimina',
      save: 'Salva',
      cancel: 'Annulla',
    },
    users: {
      title: 'Utenti',
      columns: {
        userName: 'Username',
        password: 'Password',
        roles: 'Ruoli',
      },
      dialog: {
        createTitle: 'Crea utente',
        editTitle: 'Modifica utente',
      },
    },
    roles: {
      title: 'Ruoli',
      columns: {
        name: 'Nome ruolo',
      },
      dialog: {
        createTitle: 'Crea ruolo',
        editTitle: 'Modifica ruolo',
      },
    },
    feedback: {
      warningTitle: 'Attenzione',
      genericError: 'Operazione non completata.',
      userCreated: 'Utente creato correttamente.',
      userUpdated: 'Utente aggiornato correttamente.',
      userDeleted: 'Utente eliminato correttamente.',
      roleCreated: 'Ruolo creato correttamente.',
      roleUpdated: 'Ruolo aggiornato correttamente.',
      roleDeleted: 'Ruolo eliminato correttamente.',
      confirmDeleteUser: 'Confermi l\'eliminazione dell\'utente?',
      confirmDeleteRole: 'Confermi l\'eliminazione del ruolo?',
    },
  },
  security: {
    layout: {
      title: 'Gestione Security',
      description: 'Amministra utenti, ruoli e permessi dell\'applicazione.',
    },
    common: {
      edit: 'Modifica',
      delete: 'Elimina',
      save: 'Salva',
      cancel: 'Annulla',
      manage: 'Gestisci',
      status: 'Stato',
      warning: 'Attenzione',
      error: 'Operazione non riuscita',
    },
    users: {
      title: 'Utenti',
      columns: {
        userName: 'Username',
        password: 'Password',
        roles: 'Ruoli',
      },
      details: {
        title: 'Dettagli utente',
      },
      userRoles: {
        title: 'Ruoli utente',
        manage: 'Gestisci ruoli',
      },
      actions: {
        create: 'Nuovo utente',
      },
      dialog: {
        createTitle: 'Crea nuovo utente',
        editTitle: 'Modifica utente',
      },
      validation: {
        usernameRequired: 'Username obbligatorio',
        passwordRequired: 'Password obbligatoria',
      },
      feedback: {
        created: 'Utente creato correttamente.',
        updated: 'Utente aggiornato correttamente.',
        deleted: 'Utente eliminato correttamente.',
        rolesUpdated: 'Ruoli utente aggiornati correttamente.',
        confirmDelete: 'Confermi l\'eliminazione dell\'utente?',
      },
    },
    roles: {
      title: 'Ruoli',
      columns: {
        name: 'Nome ruolo',
      },
      roleUsers: {
        title: 'Utenti con questo ruolo',
      },
      actions: {
        create: 'Nuovo ruolo',
      },
      dialog: {
        createTitle: 'Crea nuovo ruolo',
        editTitle: 'Modifica ruolo',
      },
      validation: {
        nameRequired: 'Nome ruolo obbligatorio',
      },
      feedback: {
        created: 'Ruolo creato correttamente.',
        updated: 'Ruolo aggiornato correttamente.',
        deleted: 'Ruolo eliminato correttamente.',
        confirmDelete: 'Confermi l\'eliminazione del ruolo?',
      },
    },
  },
} as const

export default it
