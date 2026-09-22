using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using MuseumAdmin.Models;
using System.Security.Claims;
using System.Text.Json;

namespace MuseumAdmin.Services
{
    public class AppState
    {
        public int MuseumId { get; private set; }
        public int UserId { get; private set; }
        public string? Token { get; private set; }
        public string? UserName { get; private set; }
        public string? Role { get; private set; }
        public string? MuseumName { get; set; }

        private readonly AuthenticationStateProvider _authStateProvider;

        public AppState(AuthenticationStateProvider authStateProvider)
        {
            _authStateProvider = authStateProvider;
        }

        private Task? _authInitTask;

        public async Task EnsureInitializedAsync()
        {
            if (_authInitTask != null)
            {
                await _authInitTask;
                return;
            }

            _authInitTask = PerformEnsureInitializedAsync();
            await _authInitTask;
        }

        private async Task PerformEnsureInitializedAsync()
        {
            if (MuseumId != 0 && UserId != 0) return;

            var state = await _authStateProvider.GetAuthenticationStateAsync();
            var user = state.User;

            if (user.Identity?.IsAuthenticated == true)
            {
                var midClaim = user.FindFirst("MuseumId")?.Value;
                if (int.TryParse(midClaim, out var mid))
                {
                    MuseumId = mid;
                }

                var uidClaim = user.FindFirst("UserId")?.Value;
                if (int.TryParse(uidClaim, out var uid))
                {
                    UserId = uid;
                }

                Token = user.FindFirst("Token")?.Value;
                Role = user.FindFirst(ClaimTypes.Role)?.Value;
                UserName = user.Identity.Name;
                MuseumName = user.FindFirst("MuseumName")?.Value;
            }
        }

        public Dictionary<int, int> LastSeenMessageIds { get; } = new();
        public Dictionary<int, int> ChatUnreadCounts { get; } = new();
        public Dictionary<int, int> ReviewTimeSecondsPerChild { get; } = new();
        public Dictionary<int, DateTime> LastReviewActivityPerChild { get; } = new();
        public Dictionary<int, DateTime?> ReviewStartTimePerChild { get; } = new();

        /// <summary>Tracks the UTC time when a chat window was opened for a specific patient.</summary>
        public Dictionary<int, DateTime> ChatSessionStartTimePerChild { get; } = new();

        /// <summary>Stores clinical note drafts per child for the current session.</summary>
        public Dictionary<int, ClinicalNoteVersion> ClinicalNoteDrafts { get; } = new();

        /// <summary>Tracks which children have their review timer paused.</summary>
        public Dictionary<int, bool> PausedReviewTimers { get; } = new();
        public int TotalUnreadMessages => ChatUnreadCounts.Values.Sum();
        public event Action? OnChatUnreadChanged;

        public void StartReviewSession(int childId)
        {
            ReviewStartTimePerChild[childId] = DateTime.UtcNow;
            LastReviewActivityPerChild[childId] = DateTime.UtcNow;
        }

        public void ResetReviewTime(int childId)
        {
            ReviewTimeSecondsPerChild[childId] = 0;
            LastReviewActivityPerChild[childId] = DateTime.UtcNow;
            ReviewStartTimePerChild[childId] = null;
            ChatSessionStartTimePerChild.Remove(childId);
            PausedReviewTimers.Remove(childId);
        }

        public void PauseReviewTimer(int childId) => PausedReviewTimers[childId] = true;
        public void ResumeReviewTimer(int childId) => PausedReviewTimers.Remove(childId);
        public bool IsReviewTimerPaused(int childId) => PausedReviewTimers.GetValueOrDefault(childId, false);

        /// <summary>
        /// Records the moment a chat window is opened for a patient.
        /// Call this when the chat drawer becomes visible for a specific patient.
        /// </summary>
        public void StartChatSession(int childId)
        {
            ChatSessionStartTimePerChild[childId] = DateTime.UtcNow;
            if (!ReviewTimeSecondsPerChild.ContainsKey(childId))
                ReviewTimeSecondsPerChild[childId] = 0;
            LastReviewActivityPerChild[childId] = DateTime.UtcNow;
        }

        /// <summary>
        /// Flushes accumulated chat time into ReviewTimeSecondsPerChild and clears the chat start marker.
        /// Call this when the chat drawer is closed for a specific patient.
        /// </summary>
        public void FlushChatSessionTime(int childId)
        {
            if (ChatSessionStartTimePerChild.TryGetValue(childId, out var startTime))
            {
                var elapsed = (int)(DateTime.UtcNow - startTime).TotalSeconds;
                if (!ReviewTimeSecondsPerChild.ContainsKey(childId))
                    ReviewTimeSecondsPerChild[childId] = 0;
                ReviewTimeSecondsPerChild[childId] += elapsed;
                LastReviewActivityPerChild[childId] = DateTime.UtcNow;
                ChatSessionStartTimePerChild.Remove(childId);
            }
        }

        private Task? _initializeSyncTask;

        public async Task InitializeUnreadCountsAsync(ChatService chatService, IJSRuntime jsRuntime)
        {
            if (_initializeSyncTask != null)
            {
                await _initializeSyncTask;
                return;
            }

            _initializeSyncTask = PerformInitializationAsync(chatService, jsRuntime);
            await _initializeSyncTask;
        }

        private async Task PerformInitializationAsync(ChatService chatService, IJSRuntime jsRuntime)
        {
            await EnsureInitializedAsync();
            if (UserId == 0) return;

            try
            {
                // 1. Restore last seen messages from storage
                var payload = await jsRuntime.InvokeAsync<string>("chatUnreadStorage.getLastSeenMessages");
                if (!string.IsNullOrEmpty(payload))
                {
                    var rawEntries = JsonSerializer.Deserialize<Dictionary<string, int>>(payload);
                    if (rawEntries != null)
                    {
                        var normalizedEntries = rawEntries
                            .Where(entry => int.TryParse(entry.Key, out _))
                            .ToDictionary(entry => int.Parse(entry.Key), entry => entry.Value);
                        
                        SetLastSeenMessages(normalizedEntries);
                    }
                }

                // 2. Fetch current chat details
                var userChatDetails = await chatService.GetUserChatDetailsAsync(UserId);
                
                // 3. Recalculate unread counts
                var unreadCounts = userChatDetails.ToDictionary(
                    chatGroup => chatGroup.ChatGroupId,
                    chatGroup =>
                    {
                        // Use server-side IsRead flag, filter out messages sent by current user
                        return chatGroup.Messages.Count(m => !m.IsRead && m.SenderId != UserId);
                    });

                SetChatUnreadCounts(unreadCounts);
                
                var total = unreadCounts.Values.Sum();
                if (total > 0)
                {
                    var details = string.Join(", ", unreadCounts.Where(x => x.Value > 0).Select(x => $"[Group {x.Key}: {x.Value} unread]"));
                    Console.WriteLine($"[DEBUG] AppState Initialized with {total} unread. Details: {details}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppState] Failed to initialize unread counts: {ex.Message}");
                _initializeSyncTask = null; // Allow retry on failure
            }
        }

        public void SetLastSeenMessages(Dictionary<int, int>? lastSeenMessages)
        {
            LastSeenMessageIds.Clear();

            if (lastSeenMessages == null)
            {
                return;
            }

            foreach (var entry in lastSeenMessages)
            {
                LastSeenMessageIds[entry.Key] = entry.Value;
            }
        }

        public void SetChatUnreadCounts(Dictionary<int, int>? unreadCounts)
        {
            ChatUnreadCounts.Clear();

            if (unreadCounts != null)
            {
                foreach (var entry in unreadCounts)
                {
                    ChatUnreadCounts[entry.Key] = Math.Max(0, entry.Value);
                }
            }

            OnChatUnreadChanged?.Invoke();
        }

        public void MarkGroupAsRead(int groupId, int latestMessageId)
        {
            LastSeenMessageIds[groupId] = latestMessageId;
        }

        public int GetLastSeenMessageId(int groupId)
        {
            return LastSeenMessageIds.TryGetValue(groupId, out var lastSeenMessageId)
                ? lastSeenMessageId
                : 0;
        }

        public void UpdateGroupTotals(List<ChatGroupDto> groups)
        {
        }

        public int GetUnreadCount(int groupId, int currentTotalMessages, int? apiUnreadCount = null)
        {
            if (ChatUnreadCounts.TryGetValue(groupId, out var unreadCount))
            {
                return unreadCount;
            }

            return Math.Max(0, apiUnreadCount ?? 0);
        }

        /// <summary>
        /// Centralized authorization check for UI routes (RBAC).
        /// Defines which roles are allowed to access which route segments.
        /// </summary>
        public bool IsAuthorizedForPath(string path)
        {
            if (string.IsNullOrEmpty(path)) path = "";
            
            // 1. Strip query string (e.g. ?ReturnUrl=...)
            var cleanedPath = path.Contains('?') ? path.Split('?')[0] : path;
            
            // 2. Normalize and check root
            var normalizedPath = cleanedPath.Trim('/').ToLower();
            if (string.IsNullOrEmpty(normalizedPath)) normalizedPath = "home";

            // 3. Universal Allowlist (Common for everyone, even unauthenticated)
            string[] universalAllowed = { "login", "error", "notfound" };
            if (universalAllowed.Any(r => normalizedPath == r || normalizedPath.StartsWith(r + "/")))
            {
                return true;
            }

            // 4. If not authenticated, let AuthorizeRouteView handle it (likely redirect to login)
            if (string.IsNullOrEmpty(Role))
            {
                return true; // Return true to allow fallback to standard auth mechanisms
            }

            // 5. Shared Logged-in Sections
            string[] sharedAllowed = { "users", "connect", "bulk-import", "dashboard", "settings" };
            if (sharedAllowed.Any(r => normalizedPath == r || normalizedPath.StartsWith(r + "/")))
            {
                return true;
            }

            // 6. Role-Specific Allowlist
            if (Role == "Therapist")
            {
                // Therapists see Programs, Child, Check-in, Connect, Dashboard, and Settings
                string[] therapistAllowed = { "programs", "child", "check-in", "connect", "dashboard", "settings" };
                return therapistAllowed.Any(r => normalizedPath == r || normalizedPath.StartsWith(r + "/"));
            }
            else
            {
                // Admin / Other staff see Gallery sections, admin tools, and settings
                string[] staffAllowed = { "home", "dashboard", "exhibits", "admin", "settings" };
                return staffAllowed.Any(r => normalizedPath == r || normalizedPath.StartsWith(r + "/"));
            }
        }
    }
}
