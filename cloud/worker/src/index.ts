import type { TurnRequest } from "../../../Contracts/rhino-copilot-protocol";
import { SCHEMA_VERSION } from "../../../Contracts/rhino-copilot-protocol";
import { handleTurn } from "../../agents/orchestrator/index";
import { isAuthorizedPluginRequest } from "../../shared/auth";
import type { Env } from "../../shared/env";

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);

    if (request.method === "GET" && url.pathname === "/health") {
      return json({
        ok: true,
        service: "rhino-copilot-cloud",
        schema_version: SCHEMA_VERSION
      });
    }

    if (request.method === "POST" && url.pathname === "/turn") {
      if (!isAuthorizedPluginRequest(request, env)) {
        return json(
          {
            ok: false,
            error: "Unauthorized plugin request."
          },
          401
        );
      }

      let payload: TurnRequest;
      try {
        payload = (await request.json()) as TurnRequest;
      } catch {
        return json(
          {
            ok: false,
            error: "Invalid JSON payload."
          },
          400
        );
      }

      if (payload.schema_version !== SCHEMA_VERSION) {
        return json(
          {
            ok: false,
            error: `Unsupported schema version '${payload.schema_version}'.`
          },
          400
        );
      }

      const response = await handleTurn(payload, env);
      return json(response);
    }

    return json(
      {
        ok: false,
        error: "Not found."
      },
      404
    );
  }
};

function json(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data, null, 2), {
    status,
    headers: {
      "content-type": "application/json; charset=utf-8"
    }
  });
}
