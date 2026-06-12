import type { Env } from "./env";
import { requireSharedSecret } from "./env";

export function isAuthorizedPluginRequest(request: Request, env: Env): boolean {
  const expected = requireSharedSecret(env);
  const actual = request.headers.get("x-plugin-secret")?.trim();
  return !!actual && actual === expected;
}

