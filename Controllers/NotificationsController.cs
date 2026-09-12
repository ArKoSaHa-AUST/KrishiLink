using System.Security.Claims;
using KrishiLink.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;

namespace KrishiLink.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notifications;
        private readonly IReminderService _reminders;
        private readonly IHostEnvironment _env;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public NotificationsController(
            INotificationService notifications,
            IReminderService reminders,
            IHostEnvironment env,
            IStringLocalizer<SharedResource> localizer)
        {
            _notifications = notifications;
            _reminders = reminders;
            _env = env;
            _localizer = localizer;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        /// <summary>
        /// GET: /Notifications — full paginated list of user notifications with unread filtering.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(bool? unreadOnly, int page = 1, int pageSize = 15)
        {
            var model = await _notifications.GetPaginatedNotificationsAsync(CurrentUserId, unreadOnly, page, pageSize);
            return View(model);
        }

        /// <summary>
        /// GET: /Notifications/UnreadCount — lightweight JSON endpoint for navbar polling.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            var count = await _notifications.GetUnreadCountAsync(CurrentUserId);
            return Json(new { count });
        }

        /// <summary>
        /// GET: /Notifications/Recent — JSON endpoint for navbar dropdown list.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Recent(int take = 5)
        {
            var items = await _notifications.GetRecentNotificationsAsync(CurrentUserId, take);
            var unreadCount = await _notifications.GetUnreadCountAsync(CurrentUserId);
            return Json(new { unreadCount, items });
        }

        /// <summary>
        /// GET: /Notifications/Open/12 — marks notification as read and navigates directly to its destination URL.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Open(int id)
        {
            var notification = await _notifications.GetNotificationAsync(id, CurrentUserId);
            if (notification != null)
            {
                await _notifications.MarkAsReadAsync(id, CurrentUserId);
                if (!string.IsNullOrWhiteSpace(notification.LinkUrl) && Url.IsLocalUrl(notification.LinkUrl))
                {
                    return Redirect(notification.LinkUrl);
                }
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// POST: /Notifications/MarkAsRead — marks a specific notification as read.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id, string? returnUrl = null)
        {
            await _notifications.MarkAsReadAsync(id, CurrentUserId);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var unreadCount = await _notifications.GetUnreadCountAsync(CurrentUserId);
                return Json(new { success = true, unreadCount });
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// POST: /Notifications/MarkAllAsRead — marks all notifications for current user as read.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead(string? returnUrl = null)
        {
            await _notifications.MarkAllAsReadAsync(CurrentUserId);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, unreadCount = 0 });
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// POST: /Notifications/Delete — deletes a notification.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? returnUrl = null)
        {
            var deleted = await _notifications.DeleteNotificationAsync(id, CurrentUserId);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var unreadCount = await _notifications.GetUnreadCountAsync(CurrentUserId);
                return Json(new { success = deleted, unreadCount });
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// POST: /Notifications/RunReminders — Development-only trigger to run due reminders for the current user.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunReminders(CancellationToken ct)
        {
            if (!_env.IsDevelopment()) return NotFound();

            var summary = await _reminders.SendDueRemindersAsync(ct, onlyUserId: CurrentUserId);
            var format = _localizer["[Dev] Reminders: {0} sent, {1} already delivered."].Value;
            TempData["SuccessMessage"] = string.Format(format, summary.Sent, summary.Skipped);

            return RedirectToAction(nameof(Index));
        }
    }
}
