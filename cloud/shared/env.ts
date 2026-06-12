export interface Env {
  OPENAI_API_KEY?: string;
  OPENAI_BASE_URL?: string;
  OPENAI_MODEL?: string;
  PLUGIN_SHARED_SECRET?: string;
  SESSION_STORE_KIND?: string;
  TRACE_STORE_KIND?: string;
}

export function requireSharedSecret(env: Env): string {
  const secret = env.PLUGIN_SHARED_SECRET?.trim();
  if (!secret) {
    throw new Error("PLUGIN_SHARED_SECRET is not configured.");
  }

  return secret;
}

