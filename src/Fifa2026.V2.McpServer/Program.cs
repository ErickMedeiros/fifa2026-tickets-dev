using Fifa2026.V2.McpServer.Data;
using Fifa2026.V2.McpServer.Infrastructure;
using Fifa2026.V2.McpServer.Llm;
using Fifa2026.V2.McpServer.Tools;

// =============================================================================
// Fifa2026.V2.McpServer — MCP Server (Story 2.5 / F5).
//
// Expõe 7 tools MCP read-only de consulta (consultar_disponibilidade, verificar_ingresso,
// consultar_bracket, consultar_partidas, consultar_classificacao, consultar_time,
// consultar_estadio — classe FifaTicketTools), TODAS ReadOnly=true, sobre o SQL do FIFA
// 2026 Tickets, via o SDK C# oficial pinado em 1.4.0 EXATO (ADE-002 Inv 1). A "mão" de
// ação (criar_alerta_ingresso) foi REMOVIDA na Story 3.1 (ADE-008): o chatbot é só
// "sentidos" e a regra de ouro vale por construção (zero superfície de escrita). O
// endpoint /mcp é Streamable HTTP
// (MapMcp() do ModelContextProtocol.AspNetCore — ADE-002 Inv 2); o framing
// JSON-RPC 2.0 e o dispatch tools/list / tools/call são do SDK (AC-15: não
// reimplementamos o protocolo à mão).
//
// SEPARADO de src/Fifa2026.V2.Functions/ (compra) — é um microserviço próprio,
// host recomendado: Azure Container App (ADE-002 Inv 2; servidor HTTP de longa
// duração que serve streaming).
//
// ACESSO: o McpServer fica ATRÁS do gateway YARP — não é chamado direto do
// browser. O gateway valida o Bearer Entra e propaga X-Entra-OID (ADE-004/
// ADE-005). O McpServer LÊ o header só para logging/personalização e NUNCA
// revalida o JWT (gateway é o guardião único — AC-9 / Task 3.8).
//
// A integração LLM (Gemini/Groq/Mistral) NÃO vive aqui — é do frontend React
// (ADE-002 Inv 3). Este servidor só expõe as tools.
// =============================================================================

var builder = WebApplication.CreateBuilder(args);

// IHttpContextAccessor — necessário para as tools lerem X-Entra-OID da request
// corrente (EntraOidContext). MapMcp roteia a tool call dentro do escopo HTTP.
builder.Services.AddHttpContextAccessor();

// Data layer — Dapper + Microsoft.Data.SqlClient, MESMO padrão de
// src/Fifa2026.V2.Functions/Data/ (queries parametrizadas, somente leitura).
builder.Services.AddSingleton<IFifaQueryRepository, FifaQueryRepository>();

// Contexto de identidade (lê/mascarar X-Entra-OID). Scoped: uma instância por request.
builder.Services.AddScoped<EntraOidContext>();

// HttpClient para o proxy de LLM (injeta a key server-side; ver LlmProxyEndpoints).
builder.Services.AddHttpClient("llm", client => client.Timeout = TimeSpan.FromSeconds(30));

// ADE-002 Inv 1/2 — registra o MCP server com transporte HTTP (Streamable HTTP)
// e descobre as tools marcadas com [McpServerToolType]/[McpServerTool] no assembly.
// API confirmada contra ModelContextProtocol(.AspNetCore) 1.4.0 por reflexão:
//   AddMcpServer() → WithHttpTransport() → WithToolsFromAssembly().
builder.Services
    .AddMcpServer()
    // Stateless: cada POST /mcp é autossuficiente (tools/call sem initialize prévio
    // nem Mcp-Session-Id). O cliente do frontend (src/lib/mcpClient.ts) faz tools/call
    // single-shot — modo stateful exigiria handshake initialize+session e retornaria
    // 400 ("A new session can only be created by an initialize request").
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly();

// Observabilidade de borda (ADE-000 Inv 5) — no-op sem APPLICATIONINSIGHTS_CONNECTION_STRING.
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

// Story 4.2 (ADE-009 Inv 1) — trava X-Gateway-Key ANTES das rotas de negócio (/mcp, /llm).
// Prova que a request veio do gateway (fail-closed quando o segredo está configurado; legado
// quando vazio). Fica ANTES dos MapMcp/MapLlmProxy para short-circuitar 401 sem processar a
// rota; o /health é isento dentro do próprio middleware (AC-8). O gateway segue o guardião
// ÚNICO do JWT (ADE-004): isto prova a ORIGEM, não valida o token.
app.UseMiddleware<GatewayKeyValidationMiddleware>();

// Health endpoint para smoke test / Container App health probe (paridade com o gateway).
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "mcp-server" }));

// ADE-002 Inv 2 — endpoint MCP (Streamable HTTP) na rota /mcp. O SDK registra o
// POST (JSON-RPC) e, em modo stateful, GET/DELETE para streaming/cleanup.
app.MapMcp("/mcp");

// Story 2.5 / F5 — proxy de LLM mínimo (decisão de segurança da key, NUNCA no
// bundle). Rotas /llm/{provider}/{*path} injetam a key (App Setting) e encaminham
// ao endpoint oficial pinado de cada provider (ADE-002 Inv 3).
app.MapLlmProxy();

app.Run();

// Necessário para WebApplicationFactory<Program> nos testes de integração.
public partial class Program { }
