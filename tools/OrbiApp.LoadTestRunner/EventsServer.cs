using Microsoft.AspNetCore.SignalR;

namespace OrbiApp.LoadTestRunner;

public sealed class LabEventsHub : Hub { }

public static class EventsServer
{
    private const string Dashboard = """
        <!doctype html>
        <html lang="es">
        <head>
          <meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
          <title>Orbi Lab · eventos</title>
          <style>
            :root { color-scheme: light dark; font-family: system-ui, sans-serif; --primary: #6750a4; --on-primary: #fff; --surface: #f7f2fa; --on-surface: #1d1b20; --outline: #79747e; --success: #146c3a; --error: #b3261e; --warning: #765a00; }
            body { max-width: 960px; margin: 2rem auto; padding: 0 1rem; background: var(--surface); color: var(--on-surface); }
            header { display: flex; align-items: center; gap: 1rem; flex-wrap: wrap; }
            #connection { border: 1px solid var(--outline); border-radius: 1rem; padding: .3rem .7rem; }
            #events { padding: 0; list-style: none; }
            li { border-top: 1px solid color-mix(in srgb, var(--outline) 35%, transparent); padding: .8rem .3rem; display: grid; grid-template-columns: 13rem 7rem 1fr; gap: .75rem; }
            .PASS, .COMPLETED { color: var(--success); } .FAIL, .INFRA_ERROR { color: var(--error); } .RUNNING { color: var(--warning); }
            button { min-height: 2.75rem; padding: .5rem 1rem; border: 0; border-radius: .75rem; background: var(--primary); color: var(--on-primary); cursor: pointer; }
            :focus-visible { outline: 3px solid var(--primary); outline-offset: 3px; }
            @media (max-width: 600px) { li { grid-template-columns: 1fr; gap: .25rem; } }
          </style>
        </head>
        <body>
          <header><h1>Orbi Lab · eventos</h1><span id="connection" role="status">Conectando…</span><button id="reconnect" type="button">Reconectar</button></header>
          <p>Actualizaciones del runner y del laboratorio. Las métricas de carga se muestran en Grafana.</p>
          <ol id="events" aria-live="polite"></ol>
          <script>
          const list = document.querySelector('#events');
          const state = document.querySelector('#connection');
          let socket;
          async function connect() {
            state.textContent = 'Conectando…';
            try {
              const response = await fetch('/events/negotiate?negotiateVersion=1', { method: 'POST' });
              if (!response.ok) throw new Error('negociación fallida');
              const negotiation = await response.json();
              const scheme = location.protocol === 'https:' ? 'wss:' : 'ws:';
              socket = new WebSocket(`${scheme}//${location.host}/events?id=${encodeURIComponent(negotiation.connectionToken)}`);
              let buffer = '';
              socket.onmessage = (event) => {
                buffer += event.data;
                const frames = buffer.split('\x1e'); buffer = frames.pop();
                for (const frame of frames) {
                  if (!frame || frame === '{}') continue;
                  try {
                    const message = JSON.parse(frame);
                    if (message.type === 1 && message.target === 'labEvent') render(message.arguments[0]);
                  } catch (_) { /* Ignore protocol handshake frames. */ }
                }
              };
              socket.onopen = () => { state.textContent = 'Conectado'; socket.send('{"protocol":"json","version":1}\x1e'); };
              socket.onclose = () => { state.textContent = 'Desconectado'; };
              socket.onerror = () => { state.textContent = 'Error de conexión'; };
            } catch (_) { state.textContent = 'No disponible'; }
          }
          function render(item) {
            const row = document.createElement('li');
            const at = document.createElement('time'); at.dateTime = item.atUtc; at.textContent = new Date(item.atUtc).toLocaleString();
            const status = document.createElement('strong'); status.className = item.status; status.textContent = `${item.kind}: ${item.status}`;
            const detail = document.createElement('span'); detail.textContent = [item.message, item.runId, item.replicas ? `${item.replicas} réplicas` : '', item.vus ? `${item.vus} VUs` : '', item.version].filter(Boolean).join(' · ');
            row.append(at, status, detail); list.prepend(row);
            while (list.children.length > 200) list.lastElementChild.remove();
          }
          document.querySelector('#reconnect').addEventListener('click', () => { if (socket) socket.close(); connect(); });
          connect();
          </script>
        </body>
        </html>
        """;

    public static async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("ORBI_LAB_EVENTS_BIND") ?? "http://127.0.0.1:15190");
        builder.Services.AddSignalR(options => options.MaximumReceiveMessageSize = 16 * 1024);
        var app = builder.Build();
        app.MapGet("/", () => Results.Content(Dashboard, "text/html; charset=utf-8"));
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
        app.MapHub<LabEventsHub>("/events");
        app.MapPost("/api/events", async (LabEvent message, IHubContext<LabEventsHub> hub, CancellationToken token) =>
        {
            if (string.IsNullOrWhiteSpace(message.Kind) || message.Kind.Length > 40 ||
                string.IsNullOrWhiteSpace(message.Status) || message.Status.Length > 40 || message.Message.Length > 500 ||
                message.RunId?.Length > 80 || message.Version?.Length > 128)
                return Results.BadRequest();
            await hub.Clients.All.SendAsync("labEvent", message, token);
            return Results.Accepted();
        }).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(16 * 1024));
        await app.RunAsync(cancellationToken);
    }
}
