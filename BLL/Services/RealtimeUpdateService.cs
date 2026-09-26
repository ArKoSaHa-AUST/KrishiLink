using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KrishiLink.BLL.Services
{
    public class RealtimeEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string EventType { get; set; } = "update"; // notification, booking, revenue, weather, system, webhook
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? TargetUserId { get; set; }
        public string? TargetRole { get; set; }
        public string? LinkUrl { get; set; }
        public object? Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public interface IRealtimeUpdateService
    {
        Task PublishToUserAsync(string userId, RealtimeEvent @event);
        Task PublishToRoleAsync(string role, RealtimeEvent @event);
        Task PublishToAllAsync(RealtimeEvent @event);
        Task ProcessWebhookEventAsync(string eventType, string payload, string? source = null);
        RealtimeClientSubscription Subscribe(string? userId, string? role);
        int ActiveConnectionsCount { get; }
    }

    public class RealtimeClientSubscription : IDisposable
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string? UserId { get; }
        public string? Role { get; }
        public Channel<RealtimeEvent> Channel { get; }
        private readonly Action<string> _onDispose;

        public RealtimeClientSubscription(string? userId, string? role, Action<string> onDispose)
        {
            UserId = userId;
            Role = role;
            Channel = System.Threading.Channels.Channel.CreateUnbounded<RealtimeEvent>(new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = true
            });
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            Channel.Writer.TryComplete();
            _onDispose(Id);
        }
    }

    public class RealtimeUpdateService : IRealtimeUpdateService
    {
        private readonly ConcurrentDictionary<string, RealtimeClientSubscription> _clients = new();
        private readonly ILogger<RealtimeUpdateService> _logger;

        public int ActiveConnectionsCount => _clients.Count;

        public RealtimeUpdateService(ILogger<RealtimeUpdateService> logger)
        {
            _logger = logger;
        }

        public RealtimeClientSubscription Subscribe(string? userId, string? role)
        {
            var subscription = new RealtimeClientSubscription(userId, role, id => _clients.TryRemove(id, out _));
            _clients.TryAdd(subscription.Id, subscription);
            _logger.LogInformation("Realtime client connected: {SubscriptionId} (User: {UserId}, Role: {Role}). Total active: {Count}",
                subscription.Id, userId ?? "Anonymous", role ?? "None", _clients.Count);
            return subscription;
        }

        public async Task PublishToUserAsync(string userId, RealtimeEvent @event)
        {
            if (string.IsNullOrWhiteSpace(userId)) return;
            @event.TargetUserId = userId;

            foreach (var client in _clients.Values)
            {
                if (string.Equals(client.UserId, userId, StringComparison.OrdinalIgnoreCase))
                {
                    client.Channel.Writer.TryWrite(@event);
                }
            }
            await Task.CompletedTask;
        }

        public async Task PublishToRoleAsync(string role, RealtimeEvent @event)
        {
            if (string.IsNullOrWhiteSpace(role)) return;
            @event.TargetRole = role;

            foreach (var client in _clients.Values)
            {
                if (string.Equals(client.Role, role, StringComparison.OrdinalIgnoreCase))
                {
                    client.Channel.Writer.TryWrite(@event);
                }
            }
            await Task.CompletedTask;
        }

        public async Task PublishToAllAsync(RealtimeEvent @event)
        {
            foreach (var client in _clients.Values)
            {
                client.Channel.Writer.TryWrite(@event);
            }
            await Task.CompletedTask;
        }

        public async Task ProcessWebhookEventAsync(string eventType, string payload, string? source = null)
        {
            _logger.LogInformation("Processing webhook event: {EventType} from source: {Source}", eventType, source ?? "Unknown");

            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;

                string title = "Realtime Update";
                string message = "A realtime system update occurred.";
                string? targetUserId = null;
                string? targetRole = null;
                string? linkUrl = null;

                if (root.TryGetProperty("title", out var titleElem)) title = titleElem.GetString() ?? title;
                if (root.TryGetProperty("message", out var msgElem)) message = msgElem.GetString() ?? message;
                if (root.TryGetProperty("user_id", out var userElem) || root.TryGetProperty("userId", out userElem))
                    targetUserId = userElem.GetString();
                if (root.TryGetProperty("role", out var roleElem) || root.TryGetProperty("targetRole", out roleElem))
                    targetRole = roleElem.GetString();
                if (root.TryGetProperty("link_url", out var linkElem) || root.TryGetProperty("linkUrl", out linkElem))
                    linkUrl = linkElem.GetString();

                var @event = new RealtimeEvent
                {
                    EventType = eventType,
                    Title = title,
                    Message = message,
                    TargetUserId = targetUserId,
                    TargetRole = targetRole,
                    LinkUrl = linkUrl,
                    Data = payload
                };

                if (!string.IsNullOrEmpty(targetUserId))
                {
                    await PublishToUserAsync(targetUserId, @event);
                }
                else if (!string.IsNullOrEmpty(targetRole))
                {
                    await PublishToRoleAsync(targetRole, @event);
                }
                else
                {
                    await PublishToAllAsync(@event);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse webhook payload for event type {EventType}", eventType);
                await PublishToAllAsync(new RealtimeEvent
                {
                    EventType = eventType,
                    Title = "Webhook Notification",
                    Message = "System update received.",
                    Data = payload
                });
            }
        }
    }
}
