import { appendFileSync } from "node:fs";
export default function (pi) {
  pi.on("tool_call", async (event) => {
    try {
      const keys = event.input && typeof event.input === "object" ? Object.keys(event.input) : [];
      const sample = JSON.stringify(event.input).slice(0, 400);
      appendFileSync("/tmp/omp-toolcall-diag.log", JSON.stringify({ tool: event.toolName, keys, sample }) + "\n");
    } catch {}
  });
}
