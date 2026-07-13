export interface RequestInstanceState {
  /** the request error message stack */
  errMsgStack: string[];
  [key: string]: unknown;
}
