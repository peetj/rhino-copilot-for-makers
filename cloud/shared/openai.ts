import type { Env } from "./env";

interface JsonCompletionOptions {
  systemPrompt: string;
  userPrompt: string;
  temperature?: number;
}

interface ChatCompletionResponse {
  choices?: Array<{
    message?: {
      content?: string | null;
    } | null;
  }>;
  error?: {
    message?: string | null;
  } | null;
}

export async function createJsonCompletion<T>(
  env: Env,
  options: JsonCompletionOptions
): Promise<T> {
  const apiKey = requireEnv(env.OPENAI_API_KEY, "OPENAI_API_KEY");
  const model = requireEnv(env.OPENAI_MODEL, "OPENAI_MODEL");
  const baseUrl = normalizeBaseUrl(env.OPENAI_BASE_URL);

  const response = await fetch(`${baseUrl}/chat/completions`, {
    method: "POST",
    headers: {
      authorization: `Bearer ${apiKey}`,
      "content-type": "application/json"
    },
    body: JSON.stringify({
      model,
      temperature: options.temperature ?? 0.1,
      response_format: {
        type: "json_object"
      },
      messages: [
        {
          role: "system",
          content: options.systemPrompt
        },
        {
          role: "user",
          content: options.userPrompt
        }
      ]
    })
  });

  const responseText = await response.text();
  if (!response.ok) {
    throw new Error(`OpenAI request failed (${response.status}): ${responseText}`);
  }

  const parsed = JSON.parse(responseText) as ChatCompletionResponse;
  const content = parsed.choices?.[0]?.message?.content?.trim();
  if (!content) {
    throw new Error("OpenAI returned no completion content.");
  }

  try {
    return JSON.parse(extractJsonObject(content)) as T;
  } catch (error) {
    throw new Error(`Planner JSON parse failed: ${error instanceof Error ? error.message : String(error)}`);
  }
}

function normalizeBaseUrl(value?: string): string {
  const trimmed = value?.trim() || "https://api.openai.com/v1";
  return trimmed.endsWith("/") ? trimmed.slice(0, -1) : trimmed;
}

function requireEnv(value: string | undefined, name: string): string {
  const trimmed = value?.trim();
  if (!trimmed) {
    throw new Error(`${name} is not configured.`);
  }

  return trimmed;
}

function extractJsonObject(content: string): string {
  const trimmed = content.trim();
  if (trimmed.startsWith("{") && trimmed.endsWith("}")) {
    return trimmed;
  }

  const start = trimmed.indexOf("{");
  const end = trimmed.lastIndexOf("}");
  if (start < 0 || end <= start) {
    throw new Error("No JSON object found in completion.");
  }

  return trimmed.slice(start, end + 1);
}
