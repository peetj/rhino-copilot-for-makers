# Worker Entry

This folder will contain the Cloudflare Worker entrypoint that exposes:

- `POST /turn`
- `POST /approval`
- `POST /step-event`
- `POST /step-result`

The worker should remain thin and delegate behavior to the orchestrator and supporting agents.

