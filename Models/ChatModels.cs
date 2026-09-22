using System.Globalization;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace MuseumAdmin.Models
{
    public class CreateGroupRequest
    {
        [JsonPropertyName("groupName")]
        public string GroupName { get; set; } = string.Empty;

        [JsonPropertyName("createdByType")]
        public string? CreatedByType { get; set; }

        [JsonPropertyName("createdById")]
        public int? CreatedById { get; set; }

        [JsonPropertyName("museumId")]
        public int MuseumId { get; set; }

        [JsonPropertyName("members")]
        public List<GroupMemberDto> Members { get; set; } = new();
    }

    public class GroupMemberDto
    {
        [JsonPropertyName("memberType")]
        public string MemberType { get; set; } = "Contact";

        [JsonPropertyName("memberId")]
        public int MemberId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public class ChatGroupDto
    {
        [JsonPropertyName("chatGroupId")]
        public int ChatGroupId { get; set; }

        [JsonPropertyName("groupName")]
        public string GroupName { get; set; } = string.Empty;

        [JsonPropertyName("lastMessage")]
        public string? LastMessage { get; set; }

        [JsonPropertyName("lastMessageTime")]
        public DateTime? LastMessageTime { get; set; }

        [JsonPropertyName("unreadCount")]
        public int UnreadCount { get; set; }

        [JsonPropertyName("isGroup")]
        public bool IsGroup { get; set; }

        [JsonPropertyName("totalMessages")]
        public int TotalMessages { get; set; }

        [JsonPropertyName("members")]
        public List<ChatGroupMemberDto> Members { get; set; } = new();
    }

    public class ChatGroupMemberDto
    {
        [JsonPropertyName("memberType")]
        public string? MemberType { get; set; }

        [JsonPropertyName("memberId")]
        public int MemberId { get; set; }

        [JsonPropertyName("memberName")]
        public string? Name { get; set; }
    }

    public class GroupFormData
    {
        [Required(ErrorMessage = "Group Name is required")]
        public string GroupName { get; set; } = "";
        public List<int> ProviderIds { get; set; } = new();
    }

    public class ProviderDto
    {
        [JsonPropertyName("userId")]
        public int UserId { get; set; }

        [JsonPropertyName("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [JsonPropertyName("lastName")]
        public string LastName { get; set; } = string.Empty;

        [JsonPropertyName("userName")]
        public string UserName { get; set; } = string.Empty;

        [JsonPropertyName("userEmail")]
        public string UserEmail { get; set; } = string.Empty;

        [JsonPropertyName("phoneNumber")]
        public string PhoneNumber { get; set; } = string.Empty;

        [JsonPropertyName("profileImage")]
        public string? ProfileImage { get; set; }

        public string FullName 
        {
            get 
            {
                var displayName = FormatUserName(UserName);
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName;
                }

                var name = $"{FirstName} {LastName}".Trim();
                return string.IsNullOrWhiteSpace(name)
                    ? (string.IsNullOrWhiteSpace(UserEmail) ? "Unknown Provider" : UserEmail)
                    : name;
            }
        }

        private static string FormatUserName(string? userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return string.Empty;
            }

            var normalized = userName.Trim();
            var atIndex = normalized.IndexOf('@');
            if (atIndex >= 0)
            {
                normalized = normalized[..atIndex];
            }

            normalized = normalized.Replace('.', ' ')
                                   .Replace('_', ' ')
                                   .Replace('-', ' ');

            var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return string.Empty;
            }

            var textInfo = CultureInfo.InvariantCulture.TextInfo;
            return string.Join(' ', words.Select(word => textInfo.ToTitleCase(word.ToLowerInvariant())));
        }
    }

    public class SelectableFamily
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Phone { get; set; }
        public bool IsExpanded { get; set; }
        public List<SelectableChild> Children { get; set; } = new();
    }

    public class SelectableChild
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool IsSelected { get; set; }
    }

    public class ChatGroupResponseDto
    {
        [JsonPropertyName("chatGroupId")]
        public int ChatGroupId { get; set; }

        [JsonPropertyName("groupName")]
        public string GroupName { get; set; } = string.Empty;

        [JsonPropertyName("isGroup")]
        public bool IsGroup { get; set; }

        [JsonPropertyName("isFamily")]
        public bool? IsFamily { get; set; }

        [JsonPropertyName("createdDate")]
        public DateTime CreatedDate { get; set; }

        [JsonPropertyName("members")]
        public List<ApiChatMemberDto> Members { get; set; } = new();

        [JsonPropertyName("messages")]
        public List<ApiChatMessageDto> Messages { get; set; } = new();
    }

    public class ApiChatMemberDto
    {
        [JsonPropertyName("memberType")]
        public string MemberType { get; set; } = string.Empty;

        [JsonPropertyName("memberId")]
        public int MemberId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("joinedDate")]
        public DateTime JoinedDate { get; set; }
    }

    public class ChildChatMappingDto
    {
        [JsonPropertyName("childId")]
        public int ChildId { get; set; }

        [JsonPropertyName("chatGroupId")]
        public int ChatGroupId { get; set; }

        [JsonPropertyName("messages")]
        public List<ApiChatMessageDto> Messages { get; set; } = new();
    }

    public class ParentChatMappingDto
    {
        [JsonPropertyName("parentId")]
        public int ParentId { get; set; }

        [JsonPropertyName("chatGroupId")]
        public int ChatGroupId { get; set; }

        [JsonPropertyName("messages")]
        public List<ApiChatMessageDto> Messages { get; set; } = new();
    }

    public class GroupChatMappingDto
    {
        [JsonPropertyName("chatGroupId")]
        public int ChatGroupId { get; set; }

        [JsonPropertyName("groupName")]
        public string GroupName { get; set; } = string.Empty;

        [JsonPropertyName("members")]
        public List<ApiChatMemberDto> Members { get; set; } = new();

        [JsonPropertyName("messages")]
        public List<ApiChatMessageDto> Messages { get; set; } = new();
    }

    public class ApiChatMessageDto
    {
        [JsonPropertyName("messageId")]
        public int MessageId { get; set; }

        [JsonPropertyName("senderType")]
        public string SenderType { get; set; } = string.Empty;

        [JsonPropertyName("senderId")]
        public int SenderId { get; set; }

        [JsonPropertyName("senderName")]
        public string? SenderName { get; set; }

        [JsonPropertyName("messageText")]
        public string MessageText { get; set; } = string.Empty;

        [JsonPropertyName("attachmentUrl")]
        public string? AttachmentUrl { get; set; }

        [JsonPropertyName("sentAt")]
        public DateTime SentAt { get; set; }

        [JsonPropertyName("isRead")]
        public bool IsRead { get; set; }
    }

    public class SendMessageRequest
    {
        [JsonPropertyName("chatGroupId")]
        public int ChatGroupId { get; set; }

        [JsonPropertyName("senderType")]
        public string SenderType { get; set; } = string.Empty;

        [JsonPropertyName("senderId")]
        public int SenderId { get; set; }

        [JsonPropertyName("messageText")]
        public string MessageText { get; set; } = string.Empty;

        [JsonPropertyName("attachmentUrl")]
        public string? AttachmentUrl { get; set; }
    }

    public class SendPrivateMessageRequest
    {
        [JsonPropertyName("senderType")]
        public string SenderType { get; set; } = string.Empty;

        [JsonPropertyName("senderId")]
        public int SenderId { get; set; }

        [JsonPropertyName("receiverId")]
        public int ReceiverId { get; set; }

        [JsonPropertyName("museumId")]
        public int MuseumId { get; set; }

        [JsonPropertyName("messageText")]
        public string MessageText { get; set; } = string.Empty;

        [JsonPropertyName("chatGroupId")]
        public int ChatGroupId { get; set; }

        [JsonPropertyName("isFamily")]
        public bool? IsFamily { get; set; }
    }
}
