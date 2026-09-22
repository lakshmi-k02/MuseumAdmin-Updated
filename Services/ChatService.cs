using System.Net.Http.Json;
using System.Text.Json;
using MuseumAdmin.Models;

namespace MuseumAdmin.Services
{
    public class ChatService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ChatService> _logger;

        public ChatService(HttpClient httpClient, ILogger<ChatService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        private void LogAudit(string action, string detail, string? operatorName = "System")
        {
            _logger.LogInformation("[AUDIT] {Timestamp} | Operator: {Operator} | Action: {Action} | Detail: {Detail}", 
                DateTime.UtcNow.ToString("o"), operatorName, action, detail);
        }

        public async Task<bool> CreateGroupWithMembersAsync(CreateGroupRequest request, string? operatorName = "System")
        {
            try
            {
                LogAudit("CREATE_GROUP", $"Creating group: {request.GroupName} with {request.Members.Count} members", operatorName);
                _logger.LogInformation("[DEBUG] Request Body (CreateGroup): {Json}", JsonSerializer.Serialize(request));
                
                var response = await _httpClient.PostAsJsonAsync("/api/Chat/create-group-with-members", request);
                var responseJson = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("[DEBUG] Response Body (CreateGroup) [{Status}]: {Json}", response.StatusCode, responseJson);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("API Error creating group: {Error}", responseJson);
                    LogAudit("CREATE_GROUP_FAILED", $"Error: {response.StatusCode}", operatorName);
                }
                else
                {
                    LogAudit("CREATE_GROUP_SUCCESS", $"Successfully created group: {request.GroupName}", operatorName);
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create group");
                return false;
            }
        }

        public async Task<List<ChatGroupDto>> GetGroupsByMuseumAsync(int museumId, string? operatorName = "System")
        {
            try
            {
                var url = $"/api/Chat/get-groups-by-museum/{museumId}";
                _logger.LogInformation("[DEBUG] GET Request: {Url}", url);
                
                var response = await _httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                // _logger.LogInformation("[DEBUG] Response Body (GetGroups): {Json}", json);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("API Error fetching groups: {Status} - {Error}", response.StatusCode, json);
                    return new List<ChatGroupDto>();
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var groups = JsonSerializer.Deserialize<List<ChatGroupDto>>(json, options);
                return groups ?? new List<ChatGroupDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch groups");
                return new List<ChatGroupDto>();
            }
        }
        public async Task<List<ChatGroupResponseDto>> GetUserChatsAsync(string memberType, int memberId, string? operatorName = "System")
        {
            try
            {
                var url = $"/memby/api/Chat/user-chats?memberType={memberType}&memberId={memberId}";
                Console.WriteLine($"[DEBUG] GET_USER_CHATS request: {url}");
                var response = await _httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] GET_USER_CHATS response length: {json.Length}");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("API Error fetching user chats: {Status} - {Error}", response.StatusCode, json);
                    return new List<ChatGroupResponseDto>();
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var chats = JsonSerializer.Deserialize<List<ChatGroupResponseDto>>(json, options);
                return chats ?? new List<ChatGroupResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch user chats");
                return new List<ChatGroupResponseDto>();
            }
        }

        public async Task<ApiChatMessageDto?> SendMessageAsync(SendMessageRequest request, string? operatorName = "System")
        {
            try
            {
                Console.WriteLine("\n****************************************************");
                Console.WriteLine($"[API] SENDING GROUP MESSAGE: {request.MessageText} (Group: {request.ChatGroupId})");
                Console.WriteLine("****************************************************\n");

                LogAudit("SEND_MESSAGE", $"Sending message to group {request.ChatGroupId}", operatorName);
                _logger.LogInformation("[DEBUG] Request Body (SendMessage): {Json}", JsonSerializer.Serialize(request));
                
                Console.WriteLine($"[DEBUG] SEND_MESSAGE payload: {JsonSerializer.Serialize(request)}");
                var response = await _httpClient.PostAsJsonAsync("/api/Chat/send-message", request);
                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] SEND_MESSAGE response: {json}");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("API Error sending message: {Error}", json);
                    return null;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var message = DeserializeChatMessage(json, options);
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message");
                return null;
            }
        }
        public async Task<List<ChatGroupResponseDto>> GetUserChatDetailsAsync(int contactId, string? operatorName = "System")
        {
            try
            {
                var url = $"/memby/api/Chat/get-user-chat-details/{contactId}";
                // Console.WriteLine($"[DEBUG] GET_USER_CHAT_DETAILS request: {url}");
                var response = await _httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                // Console.WriteLine($"[DEBUG] GET_USER_CHAT_DETAILS response: {json}");
                
                if (!response.IsSuccessStatusCode)
                {
                    // _logger.LogError("API Error fetching user chat details: {Status} - {Error}", response.StatusCode, json);
                    return new List<ChatGroupResponseDto>();
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var chatDetails = JsonSerializer.Deserialize<List<ChatGroupResponseDto>>(json, options);
                return chatDetails ?? new List<ChatGroupResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch user chat details");
                return new List<ChatGroupResponseDto>();
            }
        }

        public List<ChildChatMappingDto> MapPrivateChatsToChildren(int loginId, IEnumerable<ChatGroupResponseDto>? chatGroups)
        {
            if (chatGroups == null)
            {
                return new List<ChildChatMappingDto>();
            }

            return chatGroups
                .Where(chatGroup => !chatGroup.IsGroup && chatGroup.IsFamily != true)
                .Select(chatGroup =>
                {
                    var members = chatGroup.Members ?? new List<ApiChatMemberDto>();
                    var containsLoggedInUser = members.Any(member => member.MemberId == loginId);
                    if (!containsLoggedInUser)
                    {
                        return null;
                    }

                    var childMember = members.FirstOrDefault(member => member.MemberId != loginId);
                    if (childMember == null)
                    {
                        return null;
                    }

                    return new ChildChatMappingDto
                    {
                        ChildId = childMember.MemberId,
                        ChatGroupId = chatGroup.ChatGroupId,
                        Messages = (chatGroup.Messages ?? new List<ApiChatMessageDto>())
                            .OrderBy(message => message.SentAt)
                            .ToList()
                    };
                })
                .Where(mapping => mapping != null)
                .Cast<ChildChatMappingDto>()
                .ToList();
        }

        public List<ParentChatMappingDto> MapPrivateChatsToParents(int loginId, IEnumerable<ChatGroupResponseDto>? chatGroups)
        {
            if (chatGroups == null)
            {
                return new List<ParentChatMappingDto>();
            }

            return chatGroups
                .Where(chatGroup => !chatGroup.IsGroup && chatGroup.IsFamily == true)
                .Select(chatGroup =>
                {
                    var members = chatGroup.Members ?? new List<ApiChatMemberDto>();
                    var hasLoggedInUser = members.Any(member => member.MemberId == loginId);
                    if (!hasLoggedInUser)
                    {
                        return null;
                    }

                    var parentMember = members.FirstOrDefault(member => member.MemberId != loginId);
                    if (parentMember == null)
                    {
                        return null;
                    }

                    return new ParentChatMappingDto
                    {
                        ParentId = parentMember.MemberId,
                        ChatGroupId = chatGroup.ChatGroupId,
                        Messages = (chatGroup.Messages ?? new List<ApiChatMessageDto>())
                            .OrderBy(message => message.SentAt)
                            .ToList()
                    };
                })
                .Where(mapping => mapping != null)
                .Cast<ParentChatMappingDto>()
                .ToList();
        }

        public List<GroupChatMappingDto> MapGroupChats(int loginId, IEnumerable<ChatGroupResponseDto>? chatGroups)
        {
            if (chatGroups == null)
            {
                return new List<GroupChatMappingDto>();
            }

            return chatGroups
                .Where(chatGroup => chatGroup.IsGroup)
                .Where(chatGroup => (chatGroup.Members ?? new List<ApiChatMemberDto>())
                    .Any(member => member.MemberId == loginId))
                .Select(chatGroup => new GroupChatMappingDto
                {
                    ChatGroupId = chatGroup.ChatGroupId,
                    GroupName = chatGroup.GroupName,
                    Members = (chatGroup.Members ?? new List<ApiChatMemberDto>()).ToList(),
                    Messages = (chatGroup.Messages ?? new List<ApiChatMessageDto>())
                        .OrderBy(message => message.SentAt)
                        .ToList()
                })
                .ToList();
        }

        public async Task<ApiChatMessageDto?> SendPrivateMessageAsync(SendPrivateMessageRequest request, string? operatorName = "System")
        {
            try
            {
                Console.WriteLine("\n****************************************************");
                Console.WriteLine($"[API] SENDING PRIVATE MESSAGE: {request.MessageText} (Receiver: {request.ReceiverId})");
                Console.WriteLine("****************************************************\n");

                LogAudit("SEND_PRIVATE_MESSAGE", $"Sending private message to {request.ReceiverId}", operatorName);
                _logger.LogInformation("[DEBUG] Request Body (SendPrivateMessage): {Json}", JsonSerializer.Serialize(request));
                
                Console.WriteLine($"[DEBUG] SEND_PRIVATE_MESSAGE payload: {JsonSerializer.Serialize(request)}");
                var response = await _httpClient.PostAsJsonAsync("/memby/api/Chat/send-private-message", request);
                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] SEND_PRIVATE_MESSAGE response: {json}");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("API Error sending private message: {Error}", json);
                    return null;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var message = DeserializeChatMessage(json, options);
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send private message");
                return null;
            }
        }

        private ApiChatMessageDto? DeserializeChatMessage(string json, JsonSerializerOptions options)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var directMessage = JsonSerializer.Deserialize<ApiChatMessageDto>(json, options);
                if (directMessage != null && IsChatMessagePayload(directMessage))
                {
                    return directMessage;
                }
            }
            catch (JsonException)
            {
                // Fall back to wrapped response parsing below.
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                return FindChatMessage(document.RootElement, options);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Unable to parse chat message response payload");
                return null;
            }
        }

        private ApiChatMessageDto? FindChatMessage(JsonElement element, JsonSerializerOptions options)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                try
                {
                    var candidate = element.Deserialize<ApiChatMessageDto>(options);
                    if (candidate != null && IsChatMessagePayload(candidate))
                    {
                        return candidate;
                    }
                }
                catch (JsonException)
                {
                    // Keep scanning nested objects.
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        var nestedMessage = FindChatMessage(property.Value, options);
                        if (nestedMessage != null)
                        {
                            return nestedMessage;
                        }
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    var nestedMessage = FindChatMessage(item, options);
                    if (nestedMessage != null)
                    {
                        return nestedMessage;
                    }
                }
            }

            return null;
        }

        public async Task<bool> MarkAsReadAsync(int messageId, string? operatorName = "System")
        {
            try
            {
                var url = $"/memby/api/chat/mark-as-read/{messageId}";
                Console.WriteLine($"[DEBUG] MARK_AS_READ request: {url}");
                
                var response = await _httpClient.PutAsync(url, null);
                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] MARK_AS_READ response: {json}");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("API Error marking message as read: {Status} - {Error}", response.StatusCode, json);
                    return false;
                }

                LogAudit("MARK_AS_READ", $"Successfully marked message {messageId} as read", operatorName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark message as read");
                return false;
            }
        }

        private static bool IsChatMessagePayload(ApiChatMessageDto? message)
        {
            if (message == null)
            {
                return false;
            }

            return message.MessageId != 0
                || message.SenderId != 0
                || !string.IsNullOrWhiteSpace(message.MessageText)
                || message.SentAt != default;
        }
    }
}
