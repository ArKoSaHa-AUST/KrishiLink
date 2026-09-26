using System;
using System.IO;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KrishiLink.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KrishiLink.Controllers
{
    [ApiController]
    public class RealtimeController : ControllerBase
    {
        private readonly IRealtimeUpdateService _realtimeService;
        private readonly ILogger<RealtimeController> _logger;

        public RealtimeController(IRealtimeUpdateService realtimeService, ILogger<RealtimeController> logger)
        {
            _realtimeService = realtimeService;
            _logger = logger;
        }

        /// <summary>
        /// Realtime Server-Sent Events (SSE) stream. Connected by all pages across the application.
        /// </summary>
        [HttpGet("/api/realtime/stream")]
        [AllowAnonymous]
        public async Task GetRealtimeStream(CancellationToken cancellationToken)
        {
            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache, no-transform");
            Response.Headers.Append("Connection", "keep-alive");
            Response.Headers.Append("X-Accel-Buffering", "no");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = User.FindFirstValue(ClaimTypes.Role);

            using var subscription = _realtimeService.Subscribe(userId, role);

            // Send initial connection greeting
            var connectedPayload = JsonSerializer.Serialize(new
            {
                type = "connected",
                userId,
                role,
                activeConnections = _realtimeService.ActiveConnectionsCount,
                timestamp = DateTime.UtcNow
            });

            await Response.WriteAsync($"event: connected\ndata: {connectedPayload}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Read next event or send heartbeat every 15s
                    var readTask = subscription.Channel.Reader.ReadAsync(cancellationToken).AsTask();
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);

                    var completed = await Task.WhenAny(readTask, timeoutTask);

                    if (completed == readTask && readTask.IsCompletedSuccessfully)
                    {
                        var @event = await readTask;
                        var json = JsonSerializer.Serialize(@event);
                        await Response.WriteAsync($"event: {@event.EventType}\ndata: {json}\n\n", cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
                    }
                    else if (completed == timeoutTask)
                    {
                        // Heartbeat ping comment to keep connection alive
                        await Response.WriteAsync($": heartbeat {DateTime.UtcNow:O}\n\n", cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal client disconnect
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SSE connection ended for subscription {Id}", subscription.Id);
            }
        }

        /// <summary>
        /// Ingests Webhook events (e.g., Supabase Database Webhooks, Stripe/bKash Payment Webhooks, IoT updates).
        /// </summary>
        [HttpPost("/api/webhook/events")]
        [AllowAnonymous]
        public async Task<IActionResult> IngestWebhook([FromQuery] string? eventType = "webhook", [FromHeader(Name = "X-Webhook-Source")] string? source = null)
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                return BadRequest(new { success = false, message = "Empty webhook payload" });
            }

            await _realtimeService.ProcessWebhookEventAsync(eventType ?? "webhook", body, source ?? "ExternalWebhook");
            return Ok(new { success = true, timestamp = DateTime.UtcNow });
        }

        /// <summary>
        /// Internal API endpoint to trigger a broadcast event.
        /// </summary>
        [HttpPost("/api/webhook/broadcast")]
        [Authorize]
        public async Task<IActionResult> BroadcastEvent([FromBody] RealtimeEvent @event)
        {
            if (string.IsNullOrWhiteSpace(@event.Title) && string.IsNullOrWhiteSpace(@event.Message))
            {
                return BadRequest(new { success = false, message = "Event must have title or message" });
            }

            if (!string.IsNullOrEmpty(@event.TargetUserId))
            {
                await _realtimeService.PublishToUserAsync(@event.TargetUserId, @event);
            }
            else if (!string.IsNullOrEmpty(@event.TargetRole))
            {
                await _realtimeService.PublishToRoleAsync(@event.TargetRole, @event);
            }
            else
            {
                await _realtimeService.PublishToAllAsync(@event);
            }

            return Ok(new { success = true, eventId = @event.Id });
        }

        /// <summary>
        /// Quick diagnostic endpoint to check realtime status.
        /// </summary>
        [HttpGet("/api/realtime/status")]
        [AllowAnonymous]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                status = "online",
                activeClients = _realtimeService.ActiveConnectionsCount,
                timestamp = DateTime.UtcNow
            });
        }
    }
}
