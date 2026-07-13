declare namespace Api {
  /**
   * namespace Auth
   *
   * backend api module: "identity" (`/api/identity/*`, `/api/connect/userinfo`)
   */
  namespace Auth {
    interface UserInfo {
      userId: string;
      userName: string;
      roles: string[];
      buttons: string[];
    }
  }
}
