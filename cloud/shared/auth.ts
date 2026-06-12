import type { Env } from "./env";
import { getSharedSecret } from "./env";

export function isAuthorizedPluginRequest(request: Request, env: Env): boolean {
  const expected = getSharedSecret(env);
  if (!expected) {
    return true;
  }

  const actual = request.headers.get("x-plugin-secret")?.trim();
  return !!actual && actual === expected;
}
