interface Health {
  source: string;
  type: string;
  message: string;
  wikiUrl: string;
  /** Backend check status: ok | warn | error (only non-ok rows are returned from /health). */
  status?: string;
}

export default Health;
