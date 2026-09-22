using System.Net.Http.Json;
using System.Text.Json;
using MuseumAdmin.Models;

namespace MuseumAdmin.Services
{
    public class UserService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<UserService> _logger;
        private readonly AppState _appState;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public UserService(HttpClient httpClient, ILogger<UserService> logger, AppState appState)
        {
            _httpClient = httpClient;
            _logger = logger;
            _appState = appState;
        }

        private void LogAudit(string action, string detail, string? operatorName = "System")
        {
            // HIPAA Requirement: Traceable logs (Who, What, When, Result)
            _logger.LogInformation("[AUDIT] {Timestamp} | Operator: {Operator} | Action: {Action} | Detail: {Detail}", 
                DateTime.UtcNow.ToString("o"), operatorName, action, detail);
        }

        public async Task<List<UserListDto>> GetUsersAsync(int museumId, string? operatorName = "System")
        {
            try
            {
                LogAudit("READ_USERS", $"Fetching families for MuseumId: {museumId}", operatorName);
                
                // New API: GetFamiliesByMuseum/{museumid}
                var response = await _httpClient.GetAsync($"api/GetFamiliesByMuseum/{museumId}?page=1&pageSize=2000");
                response.EnsureSuccessStatusCode();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var responseData = await response.Content.ReadFromJsonAsync<List<UserListDto>>(options);
                return responseData ?? new List<UserListDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch users");
                return new List<UserListDto>();
            }
        }

        public async Task<List<ContactListDto>> GetContactsByUserIdAsync(int userId, string? operatorName = "System")
        {
            try
            {
                LogAudit("READ_CONTACTS", $"Fetching contacts for UserId: {userId}", operatorName);
                var response = await _httpClient.GetAsync($"api/contacts?userId={userId}");
                response.EnsureSuccessStatusCode();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var responseData = await response.Content.ReadFromJsonAsync<List<ContactListDto>>(options);
                return responseData ?? new List<ContactListDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to fetch contacts for user {userId}");
                return new List<ContactListDto>();
            }
        }

        /// <summary>
        /// Saves or updates a family in the system.
        /// </summary>
        public async Task<bool> SaveFamilyAsync(AddFamilyDto familyDto, string? operatorName = "System")
        {
            try
            {
                await _appState.EnsureInitializedAsync();
                LogAudit("SAVE_FAMILY", $"Saving family: {familyDto.FirstName} {familyDto.LastName} (Phone: {familyDto.PhoneNumber})", operatorName);

                var selectedProviderIds = familyDto.ProviderIds ?? new List<int>();
                Console.WriteLine($"[UserService] SaveFamilyAsync Selected ProviderIds: {string.Join(", ", selectedProviderIds)}");
                var caregivers = (familyDto.Caregivers != null && familyDto.Caregivers.Any())
                    ? familyDto.Caregivers
                    : familyDto.Partner != null
                        ? new List<PartnerDto> { familyDto.Partner }
                        : new List<PartnerDto>();
                var includeCaregivers = familyDto.HasPartner && caregivers.Any();

                // Build an API-contract payload (Postman-compatible shape) and
                // exclude UI-only model fields that can break strict API validators.
                var payload = new
                {
                    museumId = familyDto.MuseumId,
                    firstName = familyDto.FirstName,
                    lastName = familyDto.LastName,
                    phoneNumber = $"{familyDto.CountryCode}{familyDto.PhoneNumber}".Replace(" ", ""),
                    creatorId = _appState.UserId,
                    email = familyDto.Email,
                    birthday = NormalizeSqlDate(familyDto.Birthday),
                    hasPartner = includeCaregivers,
                    providerIds = selectedProviderIds,
                    partner = includeCaregivers
                        ? new
                        {
                            firstName = caregivers[0].FirstName,
                            lastName = caregivers[0].LastName,
                            phoneNumber = caregivers[0].PhoneNumber,
                            email = caregivers[0].Email,
                            relationToChild = caregivers[0].RelationToChild
                        }
                        : null,
                    caregivers = includeCaregivers ? caregivers
                        .Select((caregiver, index) => new
                        {
                            caregiverId = caregiver.EffectiveId ?? caregiver.PartnerId,
                            firstName = caregiver.FirstName,
                            lastName = caregiver.LastName,
                            phoneNumber = caregiver.PhoneNumber,
                            email = caregiver.Email,
                            relationToChild = caregiver.RelationToChild,
                            isPrimary = index == 0,
                            isSecondary = index == 1
                        })
                        .Cast<object>()
                        .ToList() : new List<object>(),
                    children = (familyDto.Children ?? new List<ChildDto>())
                        .Select(child => new
                        {
                            gender = child.Gender,
                            firstName = child.FirstName,
                            lastName = child.LastName,
                            dob = NormalizeSqlDate(child.dob),
                            contactGender = child.Gender,
                            artTitle = child.ArtTitle,
                            messageAudioText = child.MessageAudioText,
                            imageBase64 = child.ImageBase64,
                            audioBase64 = child.AudioBase64,
                            relationToChild = child.RelationToChild,
                            relationKind = child.RelationKind,
                            contactId = child.ContactId
                        })
                        .ToList(),
                    researchId = familyDto.ResearchId,
                    enrollmentYear = familyDto.EnrollmentYear,
                    permissionMessage = familyDto.PermissionMessage
                };

                var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                Console.WriteLine("\n[API CALL] POST api/AddFamily");
                Console.WriteLine($"[REQUEST BODY]:\n{payloadJson}");
                _logger.LogInformation("AddFamily payload being sent to api/AddFamily:\n{Payload}", payloadJson);

                var response = await _httpClient.PostAsJsonAsync("api/AddFamily", payload);
                var responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[RESPONSE STATUS]: {(int)response.StatusCode} {response.StatusCode}");
                Console.WriteLine($"[RESPONSE BODY]:\n{responseBody}\n");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("API Error saving family: {Error}", responseBody);
                    LogAudit("SAVE_FAMILY_FAILED", $"Error: {response.StatusCode}", operatorName);
                }
                else
                {
                    LogAudit("SAVE_FAMILY_SUCCESS", $"Successfully saved family: {familyDto.LastName}", operatorName);
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save family");
                return false;
            }
        }

        private static DateTime NormalizeSqlDate(DateTime? value)
        {
            // SQL Server datetime min value used by backend sample payload.
            var minSqlDate = new DateTime(1753, 1, 1);
            if (!value.HasValue || value.Value < minSqlDate)
            {
                return minSqlDate;
            }

            return value.Value;
        }

        public async Task<bool> AddChildAsync(AddChildRequestDto request, string? operatorName = "System")
        {
            var result = await AddChildAsyncDetails(request, operatorName);
            return result.Success;
        }

        public async Task<(bool Success, string Endpoint, int StatusCode, string ResponseBody)> AddChildAsyncDetails(AddChildRequestDto request, string? operatorName = "System")
        {
            var fullEndpoint = new Uri(_httpClient.BaseAddress ?? new Uri("https://membyapi.azurewebsites.net/memby/"), "api/AddChild").ToString();
            try
            {
                var payloadJson = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine($"[UserService.AddChildAsync] === HTTP REQUEST ===\nEndpoint: {fullEndpoint} | Operator: {operatorName}\nPayload:\n{payloadJson}");
                _logger.LogInformation("AddChild payload: {Payload}", payloadJson);

                var response = await _httpClient.PostAsJsonAsync("api/AddChild", request);
                var responseContent = await response.Content.ReadAsStringAsync();
                int statusCode = (int)response.StatusCode;
                
                Console.WriteLine($"[UserService.AddChildAsync] === HTTP RESPONSE ===\nEndpoint: {fullEndpoint}\nStatus: {response.StatusCode} ({statusCode})\nBody:\n{responseContent}");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("API Error adding child: {Error}", responseContent);
                    LogAudit("ADD_CHILD_FAILED", $"Status: {response.StatusCode} | Error: {responseContent}", operatorName);
                }
                else
                {
                    LogAudit("ADD_CHILD_SUCCESS", $"Successfully added child to family {request.userId}", operatorName);
                }

                return (response.IsSuccessStatusCode, fullEndpoint, statusCode, responseContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserService.AddChildAsync] === HTTP EXCEPTION ===\nEndpoint: {fullEndpoint}\nMessage: {ex.Message}\nStackTrace: {ex.StackTrace}");
                _logger.LogError(ex, "Failed to add child");
                return (false, fullEndpoint, 500, ex.Message);
            }
        }

        public async Task<(bool Success, string Endpoint, int StatusCode, string ResponseBody)> UpdateChildrenAsyncDetails(UpdateChildrenRequestDto request, string? operatorName = "System")
        {
            var fullEndpoint = new Uri(_httpClient.BaseAddress ?? new Uri("https://membyapi.azurewebsites.net/memby/"), "api/UpdateChildren").ToString();
            try
            {
                var payloadJson = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine($"[UserService.UpdateChildrenAsync] === HTTP REQUEST ===\nEndpoint: PUT {fullEndpoint} | Operator: {operatorName}\nPayload:\n{payloadJson}");
                _logger.LogInformation("UpdateChildren payload: {Payload}", payloadJson);

                var response = await _httpClient.PutAsJsonAsync("api/UpdateChildren", request);
                var responseContent = await response.Content.ReadAsStringAsync();
                int statusCode = (int)response.StatusCode;
                
                Console.WriteLine($"[UserService.UpdateChildrenAsync] === HTTP RESPONSE ===\nEndpoint: PUT {fullEndpoint}\nStatus: {response.StatusCode} ({statusCode})\nBody:\n{responseContent}");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("API Error updating children: {Error}", responseContent);
                    LogAudit("UPDATE_CHILDREN_FAILED", $"Status: {response.StatusCode} | Error: {responseContent}", operatorName);
                }
                else
                {
                    LogAudit("UPDATE_CHILDREN_SUCCESS", $"Successfully updated children for family {request.userId}", operatorName);
                }

                return (response.IsSuccessStatusCode, fullEndpoint, statusCode, responseContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserService.UpdateChildrenAsync] === HTTP EXCEPTION ===\nEndpoint: PUT {fullEndpoint}\nMessage: {ex.Message}\nStackTrace: {ex.StackTrace}");
                _logger.LogError(ex, "Failed to update children");
                return (false, fullEndpoint, 500, ex.Message);
            }
        }

        public async Task<bool> DeleteFamilyAsync(int userId, string? operatorName = "System")
        {
            try
            {
                LogAudit("DELETE_FAMILY", $"Deleting family ID: {userId}", operatorName);

                // Assuming API endpoint for delete. If not exists, this will return failure.
                var response = await _httpClient.DeleteAsync($"api/DeleteFamily/{userId}");
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("API Error deleting family: {Error}", error);
                    LogAudit("DELETE_FAMILY_FAILED", $"Error: {response.StatusCode}", operatorName);
                }
                else
                {
                    LogAudit("DELETE_FAMILY_SUCCESS", $"Successfully deleted family ID {userId}", operatorName);
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete family");
                return false;
            }
        }

        /// <summary>
        /// Gets all contacts (children) with their assignment status for health giver view
        /// </summary>
        /// <param name="museumId">Museum ID to filter contacts</param>
        /// <param name="filterStatus">Optional filter: "Active", "New", or "Inactive"</param>
        /// <param name="operatorName">Name of the operator for audit logging</param>
        /// <returns>List of contacts with status and assignments</returns>
        public async Task<List<ContactWithStatusDto>> GetContactsWithStatusAsync(int museumId, string? filterStatus = null, string? operatorName = "System")
        {
            try
            {
                LogAudit("READ_CONTACTS_WITH_STATUS", $"Fetching contacts for MuseumId: {museumId}, Filter: {filterStatus ?? "All"}", operatorName);
                // 1. Get all families and provider directory for the museum
                var familiesTask = GetUsersAsync(museumId, operatorName);
                var providersTask = GetProvidersAsync(museumId, operatorName);

                await Task.WhenAll(familiesTask, providersTask);

                var families = familiesTask.Result;
                var providers = providersTask.Result;
                var providersById = providers.ToDictionary(provider => provider.UserId, provider => provider.FullName);

                _logger.LogInformation("Fetched {FamilyCount} families for MuseumId {MuseumId}", families.Count, museumId);
                // 2. Extract all contacts from families and convert to ContactWithStatusDto
                var allContacts = new List<ContactWithStatusDto>();
                foreach (var family in families)
                {
                    _logger.LogInformation("Processing family {UserId} with {ChildCount} children", family.UserId, family.Children?.Count ?? 0);
                    foreach (var child in family.Children ?? new List<FamilyMemberDto>())
                    {
                        var childName = child.Name;
                        if (string.IsNullOrWhiteSpace(childName))
                        {
                            childName = $"Child {child.ContactId}";
                        }
                        var parentName = !string.IsNullOrWhiteSpace(family.AccountOwnerName) 
                            ? family.AccountOwnerName 
                            : (!string.IsNullOrWhiteSpace(family.Name) ? family.Name : "No Name");
                        var providerNames = new List<string>();
                        var providerIds = new List<int>();

                        // 1. Process nested Providers list from JSON (new behavior)
                        if (family.Providers != null && family.Providers.Any())
                        {
                            foreach (var p in family.Providers)
                            {
                                providerIds.Add(p.Id);
                                if (!string.IsNullOrWhiteSpace(p.Name))
                                {
                                    providerNames.Add(p.Name);
                                }
                                else if (providersById.TryGetValue(p.Id, out var fallbackName))
                                {
                                    providerNames.Add(fallbackName);
                                }
                            }
                        }

                        // 2. Process ProviderIds list (legacy/fallback behavior)
                        if (family.ProviderIds != null && family.ProviderIds.Any())
                        {
                            foreach (var id in family.ProviderIds)
                            {
                                if (!providerIds.Contains(id))
                                {
                                    providerIds.Add(id);
                                }
                                
                                if (providersById.TryGetValue(id, out var name) && !providerNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                                {
                                    providerNames.Add(name);
                                }
                            }
                        }
                        
                        var contactWithStatus = new ContactWithStatusDto
                        {
                            ContactId = child.ContactId,
                            ContactName = childName,
                            UserId = family.UserId,
                            Dob = child.Dob?.ToString("O"),
                            ContactGender = child.ContactGeneder,
                            ContactAddedDatetime = child.ContactAddedDatetime?.ToString("O"),
                            ResearchId = child.ResearchId,
                            OwnerName = parentName,
                            ProviderIds = providerIds.Distinct().ToList(),
                            AssignedProviderName = providerNames.Any() ? string.Join(", ", providerNames.Distinct(StringComparer.OrdinalIgnoreCase)) : null,
                            Status = "New", // Default to New as per requirement
                            Assignments = new List<AssignedTherapyDto>(),
                            AlertCount = 0,
                            LastActivity = child.ContactAddedDatetime
                        };
                        // Calculate Age
                        if (child.Dob.HasValue)
                        {
                            var today = DateTime.Today;
                            var age = today.Year - child.Dob.Value.Year;
                            if (child.Dob.Value.Date > today.AddYears(-age)) age--;
                            contactWithStatus.Age = age.ToString();
                        }
                        allContacts.Add(contactWithStatus);
                    }
                }
                _logger.LogInformation("Total children mapped: {TotalChildren} from {FamilyCount} families", allContacts.Count, families.Count);
                // 5. Apply filter if provided
                if (!string.IsNullOrEmpty(filterStatus) && filterStatus != "All")
                {
                    allContacts = allContacts.Where(c => c.Status == filterStatus).ToList();
                }
                LogAudit("READ_CONTACTS_WITH_STATUS_SUCCESS", $"Retrieved {allContacts.Count} contacts", operatorName);
                return allContacts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch contacts with status");
                return new List<ContactWithStatusDto>();
            }
        }

        /// <summary>
        /// Gets assignments for a specific contact
        /// </summary>
        /// <param name="contactId">Contact ID</param>
        /// <param name="operatorName">Name of the operator for audit logging</param>
        /// <returns>List of assignments for the contact</returns>
        public async Task<List<AssignedTherapyDto>> GetContactAssignmentsAsync(int contactId, string? operatorName = "System")
        {
            try
            {
                LogAudit("READ_CONTACT_ASSIGNMENTS", $"Fetching assignments for ContactId: {contactId}", operatorName);
                
                var url = $"https://membyapi.azurewebsites.net/memby/api/GetAllAssignedByChild/{contactId}";
                _logger.LogInformation("[UserService] Fetching assignments from: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var assignments = await response.Content.ReadFromJsonAsync<List<AssignedTherapyDto>>(options);
                    _logger.LogInformation("[UserService] Successfully retrieved {Count} assignments for ContactId: {ContactId}", assignments?.Count ?? 0, contactId);
                    return assignments ?? new List<AssignedTherapyDto>();
                }

                _logger.LogWarning("[UserService] GetAllAssignedByChild returned non-success status: {StatusCode} for ContactId: {ContactId}", response.StatusCode, contactId);
                return new List<AssignedTherapyDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserService] Error fetching contact assignments for ContactId {contactId}: {ex.Message}");
                _logger.LogError(ex, "Failed to fetch contact assignments for ContactId: {ContactId}", contactId);
                return new List<AssignedTherapyDto>();
            }
        }

        public async Task<List<CompletedModuleDto>> GetCompletedModulesForContactAsync(int contactId, string? operatorName = "System")
        {
            try
            {
                LogAudit("READ_COMPLETED_MODULES", $"Fetching completed modules for ContactId: {contactId}", operatorName);

                var url = $"https://membyapi.azurewebsites.net/memby/api/Musium/GetCompletedModulesForContact?contactId={contactId}";
                _logger.LogInformation("[UserService] Fetching completed modules from: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return new List<CompletedModuleDto>();
                }

                var modules = await response.Content.ReadFromJsonAsync<List<CompletedModuleDto>>(_jsonOptions);
                return modules ?? new List<CompletedModuleDto>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch completed modules for contact {ContactId}", contactId);
                return new List<CompletedModuleDto>();
            }
        }

        /// <summary>
        /// Assigns a module/exercise to a contact
        /// </summary>
        /// <param name="contactId">Contact ID</param>
        /// <param name="moduleId">Module ID to assign</param>
        /// <param name="operatorName">Name of the operator for audit logging</param>
        /// <returns>True if assignment was successful</returns>
        public async Task<bool> AssignExerciseToContactAsync(int contactId, int moduleId, string? operatorName = "System", TimingSettings? timing = null)
        {
            try
            {
                LogAudit("ASSIGN_EXERCISE", $"Assigning ModuleId {moduleId} to ContactId {contactId}", operatorName);
                
                var request = new AssignExerciseRequestDto
                {
                    ContactId = contactId,
                    ModuleId = moduleId,
                    Timing = timing
                };
                
                // TODO: Replace with actual API endpoint when available
                // Placeholder: api/AssignExercise
                var url = "api/AssignExercise";
                var jsonPayload = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine($"\n[API CALL] POST {url}");
                Console.WriteLine($"[REQUEST PAYLOAD]:\n{jsonPayload}");

                var response = await _httpClient.PostAsJsonAsync(url, request);
                var responseBody = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"[RESPONSE STATUS]: {(int)response.StatusCode} {response.StatusCode}");
                Console.WriteLine($"[RESPONSE BODY]:\n{responseBody}\n");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("API Error assigning exercise: {Error}", responseBody);
                    LogAudit("ASSIGN_EXERCISE_FAILED", $"Error: {response.StatusCode}", operatorName);
                }
                else
                {
                    LogAudit("ASSIGN_EXERCISE_SUCCESS", $"Successfully assigned ModuleId {moduleId} to ContactId {contactId}", operatorName);
                }
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to assign exercise");
                return false;
            }
        }

        /// <summary>
        /// Assigns therapy (module/exercises) using the new AssignTherapy API
        /// </summary>
        public async Task<bool> AssignTherapyAsync(object request, string? operatorName = "System")
        {
            try
            {
                var detail = "Assigning therapy settings";
                LogAudit("ASSIGN_THERAPY", detail, operatorName);

                var url = "https://membyapi.azurewebsites.net/memby/api/AssignTherapy";
                
                var jsonPayload = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine("\n==================================================");
                Console.WriteLine($"[API CALL] POST {url}");
                Console.WriteLine($"[REQUEST PAYLOAD]:\n{jsonPayload}");
                Console.WriteLine("==================================================\n");
                
                var response = await _httpClient.PostAsJsonAsync(url, request);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine("\n==================================================");
                Console.WriteLine($"[API RESPONSE] POST AssignTherapy - Status: {(int)response.StatusCode} {response.StatusCode}");
                Console.WriteLine($"[RESPONSE BODY]:\n{responseBody}");
                Console.WriteLine("==================================================\n");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("API Error in AssignTherapy: {StatusCode}, Error: {Error}", response.StatusCode, responseBody);
                    LogAudit("ASSIGN_THERAPY_FAILED", $"Status: {response.StatusCode}", operatorName);
                }
                else
                {
                    LogAudit("ASSIGN_THERAPY_SUCCESS", "Successfully assigned therapy", operatorName);
                }
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call AssignTherapy API");
                return false;
            }
        }

        /// <summary>
        /// Updates therapy (module/exercises) using the UpdateTherapy POST API
        /// </summary>
        public async Task<bool> UpdateTherapyAsync(object request, string? operatorName = "System")
        {
            try
            {
                var detail = "Updating therapy settings";
                LogAudit("UPDATE_THERAPY", detail, operatorName);

                var url = "https://membyapi.azurewebsites.net/memby/api/UpdateTherapy";
                
                var jsonPayload = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine("\n==================================================");
                Console.WriteLine($"[API CALL] POST {url}");
                Console.WriteLine($"[REQUEST PAYLOAD]:\n{jsonPayload}");
                Console.WriteLine("==================================================\n");
                
                var response = await _httpClient.PostAsJsonAsync(url, request);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine("\n==================================================");
                Console.WriteLine($"[API RESPONSE] POST UpdateTherapy - Status: {(int)response.StatusCode} {response.StatusCode}");
                Console.WriteLine($"[RESPONSE BODY]:\n{responseBody}");
                Console.WriteLine("==================================================\n");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("API Error in UpdateTherapy: {StatusCode}, Error: {Error}", response.StatusCode, responseBody);
                    LogAudit("UPDATE_THERAPY_FAILED", $"Status: {response.StatusCode}", operatorName);
                }
                else
                {
                    LogAudit("UPDATE_THERAPY_SUCCESS", "Successfully updated therapy", operatorName);
                }
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call UpdateTherapy API");
                return false;
            }
        }

        /// <summary>
        /// Deassigns therapy (module) from a contact using the DeassignTherapy API
        /// </summary>
        public async Task<bool> DeassignTherapyAsync(AssignTherapyRequest request, string? operatorName = "System")
        {
            try
            {
                var detail = $"Deassigning ModuleId: {request.moduleId} for Contacts: {string.Join(",", request.assignments.Select(a => a.contactId))}";
                LogAudit("DEASSIGN_THERAPY", detail, operatorName);

                var url = "https://membyapi.azurewebsites.net/memby/api/DeassignTherapy";
                
                var jsonPayload = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine($"\n[API CALL] POST {url}");
                Console.WriteLine($"[REQUEST PAYLOAD]:\n{jsonPayload}");
                
                var response = await _httpClient.PostAsJsonAsync(url, request);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"[RESPONSE STATUS]: {(int)response.StatusCode} {response.StatusCode}");
                Console.WriteLine($"[RESPONSE BODY]:\n{responseBody}\n");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("API Error in DeassignTherapy: {StatusCode}, Error: {Error}", response.StatusCode, responseBody);
                    LogAudit("DEASSIGN_THERAPY_FAILED", $"Status: {response.StatusCode}", operatorName);
                }
                else
                {
                    LogAudit("DEASSIGN_THERAPY_SUCCESS", "Successfully deassigned therapy", operatorName);
                }
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call DeassignTherapy API");
                return false;
            }
        }

        private class AssignmentModuleApiDto
        {
            public int modulePayloadId { get; set; }
            public int moduleId { get; set; }
            public string name { get; set; } = "";
            public int messageCount { get; set; }
            public object? ageIds { get; set; } // Handle as object to be safe (string or array)
            public object? zoneIds { get; set; }
            public List<MuseumAdmin.Components.Pages.Connect.ModuleMessagesTesting>? messages { get; set; }
        }

        /// <summary>
        /// Gets available modules for assignment selection
        /// </summary>
        /// <param name="museumId">Museum ID</param>
        /// <param name="operatorName">Name of the operator for audit logging</param>
        /// <returns>List of modules available for assignment</returns>
        public async Task<List<ModuleSelectionDto>> GetAvailableModulesAsync(int museumId, string? operatorName = "System")
        {
            var totalSw = System.Diagnostics.Stopwatch.StartNew();
            const int maxRetries = 2;
            int attempt = 0;
            
            while (attempt <= maxRetries)
            {
                try
                {
                    LogAudit("READ_AVAILABLE_MODULES", $"Fetching available modules for assignment (MuseumId: {museumId}, Attempt: {attempt + 1})", operatorName);
                    
                    var url = $"https://membyapi.azurewebsites.net/memby/api/Musium/GetModulesWithMessagesWithMessages?museumId={museumId}";
                    Console.WriteLine($"\n[API CALL] GET {url}");
                    
                    var networkSw = System.Diagnostics.Stopwatch.StartNew();
                    // Use ResponseHeadersRead to start streaming content immediately
                    using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    Console.WriteLine($"[RESPONSE STATUS]: {(int)response.StatusCode} {response.StatusCode}");
                    networkSw.Stop();
                    _logger.LogInformation("[UserService] Network headers received in {ElapsedMs}ms", networkSw.ElapsedMilliseconds);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var readSw = System.Diagnostics.Stopwatch.StartNew();
                        using var responseStream = await response.Content.ReadAsStreamAsync();
                        // Wrap in a BufferedStream to improve read performance for large payloads
                        using var stream = new System.IO.BufferedStream(responseStream, 131072); // 128KB buffer
                        
                        // Streaming deserialization for memory efficiency and resilience with large payloads
                        var apiDtos = await JsonSerializer.DeserializeAsync<List<AssignmentModuleApiDto>>(stream, _jsonOptions);
                        readSw.Stop();
                        _logger.LogInformation("[UserService] Full content streamed and deserialized in {ElapsedMs}ms", readSw.ElapsedMilliseconds);
                        
                        if (apiDtos == null) return new List<ModuleSelectionDto>();

                        var mapSw = System.Diagnostics.Stopwatch.StartNew();
                        // Sort by ModulePayloadId descending (Latest first)
                        apiDtos = apiDtos.OrderByDescending(m => m.modulePayloadId > 0 ? m.modulePayloadId : m.moduleId).ToList();

                        var result = new List<ModuleSelectionDto>();
                        foreach (var m in apiDtos)
                        {
                            try 
                            {
                                int modId = m.modulePayloadId > 0 ? m.modulePayloadId : m.moduleId;
                                int count = m.messages != null && m.messages.Count > 0 ? m.messages.Count : m.messageCount;

                                var dto = new ModuleSelectionDto
                                {
                                    ModuleId = modId,
                                    Name = m.name,
                                    Description = "", 
                                    ExerciseCount = count,
                                    AgeValues = ParseIds(m.ageIds),
                                    ZoneNames = ParseIds(m.zoneIds),
                                    Messages = m.messages ?? new()
                                };
                                
                                result.Add(dto);
                            }
                            catch (Exception itemEx)
                            {
                                Console.WriteLine($"[UserService] Error mapping module {m.modulePayloadId}: {itemEx.Message}");
                            }
                        }
                        mapSw.Stop();
                        _logger.LogInformation("[UserService] Sorted and mapped {Count} modules in {ElapsedMs}ms", result.Count, mapSw.ElapsedMilliseconds);
                        _logger.LogInformation("[UserService] Total process time: {ElapsedMs}ms", totalSw.ElapsedMilliseconds);
                        
                        return result;
                    }
                    
                    return new List<ModuleSelectionDto>();
                }
                catch (Exception ex) when ((ex is HttpRequestException || ex is System.IO.IOException || ex is System.Text.Json.JsonException) && (attempt < maxRetries))
                {
                    attempt++;
                    _logger.LogWarning(ex, "Attempt {Attempt} failed for GetAvailableModulesAsync (Type: {Type}). Retrying in {Delay}ms...", attempt, ex.GetType().Name, 1000 * attempt);
                    await Task.Delay(1000 * attempt);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fatal error on attempt {Attempt} in GetAvailableModulesAsync: {Message}", attempt + 1, ex.Message);
                    return new List<ModuleSelectionDto>();
                }
            }
            
            return new List<ModuleSelectionDto>();
        }
        
        private List<string> ParseIds(object? idData)
        {
            if (idData == null) return new List<string>();
            try
            {
                if (idData is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        var json = element.GetString();
                        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
                        var ints = JsonSerializer.Deserialize<List<int>>(json);
                        return ints?.Select(i => i.ToString()).ToList() ?? new List<string>();
                    }
                    else if (element.ValueKind == JsonValueKind.Array)
                    {
                        // Handle case where API returns actual array instead of string
                        var ints = JsonSerializer.Deserialize<List<int>>(element.GetRawText());
                        return ints?.Select(i => i.ToString()).ToList() ?? new List<string>();
                    }
                }
                else if (idData is string str)
                {
                     if (string.IsNullOrWhiteSpace(str)) return new List<string>();
                     var ints = JsonSerializer.Deserialize<List<int>>(str);
                     return ints?.Select(i => i.ToString()).ToList() ?? new List<string>();
                }
            }
            catch 
            {
                // Ignore parse errors for auxiliary data
            }
            return new List<string>();
        }

        /// <summary>
        /// Gets exercises (messages) for a specific module
        /// </summary>
        /// <param name="moduleId">Module ID</param>
        /// <param name="operatorName">Name of the operator for audit logging</param>
        /// <returns>List of exercises for the module</returns>
        public async Task<List<MuseumAdmin.Components.Pages.Connect.ModuleMessagesTesting>> GetModuleExercisesAsync(int moduleId, string? operatorName = "System")
        {
            try
            {
                LogAudit("READ_MODULE_EXERCISES", $"Fetching exercises for ModuleId: {moduleId}", operatorName);
                
                // Use correct full URL
                var url = $"https://membyapi.azurewebsites.net/memby/api/Musium/GetByModuleId?moduleId={moduleId}";
                Console.WriteLine($"\n[API CALL] GET {url}");
                
                var response = await _httpClient.GetAsync(url);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                // Console.WriteLine($"[RESPONSE STATUS]: {(int)response.StatusCode} {response.StatusCode}");
                // Not logging full body for list as it might be huge, but logging length
                // Console.WriteLine($"[RESPONSE BODY LENGTH]: {responseBody.Length} chars");

                if (response.IsSuccessStatusCode)
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var exercises = JsonSerializer.Deserialize<List<MuseumAdmin.Components.Pages.Connect.ModuleMessagesTesting>>(responseBody, options);
                    return exercises ?? new List<MuseumAdmin.Components.Pages.Connect.ModuleMessagesTesting>();
                }
                
                return new List<MuseumAdmin.Components.Pages.Connect.ModuleMessagesTesting>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to fetch exercises for module {moduleId}");
                return new List<MuseumAdmin.Components.Pages.Connect.ModuleMessagesTesting>();
            }
        }

        /// <summary>
        /// Gets all Providers from the system.
        /// </summary>
        public async Task<List<ProviderDto>> GetProvidersAsync(int museumId, string? operatorName = "System")
        {
            try
            {
                LogAudit("READ_ProviderS", $"Fetching Providers for MuseumId: {museumId}", operatorName);
                var url = $"https://membyapi.azurewebsites.net/memby/api/GetTherapistsByMuseum?museumId={museumId}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var Providers = await response.Content.ReadFromJsonAsync<List<ProviderDto>>(_jsonOptions);
                return Providers ?? new List<ProviderDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch Providers");
                return new List<ProviderDto>();
            }
        }

        /// <summary>
        /// Gets therapist programs for a specific user type and target user ID.
        /// </summary>
        public async Task<List<TherapistProgramDto>> GetTherapistProgramsAsync(int museumId, int userId, int userType, string? operatorName = "System")
        {
            try
            {
                LogAudit("READ_THERAPIST_PROGRAMS", $"Fetching programs for MuseumId: {museumId}, UserId: {userId}, UserType: {userType}", operatorName);
                
                var url = "https://membyapi.azurewebsites.net//api/TherapistPrograms/get-programs";
                var request = new
                {
                    museumId = museumId,
                    assignedBy = new
                    {
                        id = _appState.UserId,
                        type = "USER"
                    },
                    assignedTo = new
                    {
                        id = userId,
                        type = "CONTACT"
                    }
                };

                var response = await _httpClient.PostAsJsonAsync(url, request);
                Console.WriteLine("************************************************");
                Console.WriteLine("************************************************");
                Console.WriteLine($"[API CALL] POST {url}");
                Console.WriteLine($"Request Payload: {JsonSerializer.Serialize(request)}");
                Console.WriteLine("************************************************");

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("API Error getting therapist programs: {Error}", error);
                    Console.WriteLine($"[API RESPONSE ERROR] Status: {response.StatusCode}, Body: {error}");
                    Console.WriteLine("************************************************");
                    Console.WriteLine("************************************************");
                    return new List<TherapistProgramDto>();
                }

                var programs = await response.Content.ReadFromJsonAsync<List<TherapistProgramDto>>(_jsonOptions);
                
                Console.WriteLine("[API RESPONSE] Loaded Programs Payload:");
                Console.WriteLine(JsonSerializer.Serialize(programs, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine("************************************************");
                Console.WriteLine("************************************************");

                return programs ?? new List<TherapistProgramDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch therapist programs");
                return new List<TherapistProgramDto>();
            }
        }
    }
}
