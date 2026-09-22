using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using MuseumAdmin.Models;

namespace MuseumAdmin.Services
{
    public class BulkExerciseImportService
    {
        private readonly HttpClient _http;
        private readonly ILogger<BulkExerciseImportService> _logger;
        private const string ApiBase = "https://membyapi.azurewebsites.net/memby/api/Musium";

        public event Action<ImportLogEntry>? OnLogAdded;
        public event Action? OnProgressChanged;

        public List<ImportLogEntry> Logs { get; } = new();
        public bool IsImportRunning { get; private set; }
        public bool IsPaused { get; private set; }

        private CancellationTokenSource? _cts;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true
        };

        public BulkExerciseImportService(HttpClient http, ILogger<BulkExerciseImportService> logger)
        {
            _http = http;
            _logger = logger;
        }

        #region Excel Parsing

        public BulkImportWorkbook ParseWorkbook(Stream stream, string fileName = "Exercise_Library.xlsx", long fileSizeBytes = 0)
        {
            var workbookModel = new BulkImportWorkbook
            {
                FileName = fileName,
                FileSizeBytes = fileSizeBytes,
                LoadedAt = DateTime.UtcNow
            };

            using var excelWorkbook = new XLWorkbook(stream);

            int sheetIndex = 0;
            foreach (var worksheet in excelWorkbook.Worksheets)
            {
                sheetIndex++;
                var sheetModel = new BulkImportSheet
                {
                    SheetName = worksheet.Name,
                    SheetIndex = sheetIndex
                };

                try
                {
                    ParseWorksheet(worksheet, sheetModel);
                }
                catch (Exception ex)
                {
                    sheetModel.ValidationErrors.Add($"Failed to parse worksheet: {ex.Message}");
                    _logger.LogError(ex, "Error parsing worksheet {SheetName}", worksheet.Name);
                }

                workbookModel.Sheets.Add(sheetModel);
            }

            return workbookModel;
        }

        private void ParseWorksheet(IXLWorksheet worksheet, BulkImportSheet sheetModel)
        {
            // 1. Module Description from cell A2
            var cellA2 = worksheet.Cell(2, 1);
            sheetModel.ModuleDescription = cellA2.GetString()?.Trim() ?? string.Empty;

            // 2. Dynamic header row scanning (scan first 10 rows for Col A contains "Module" and Col E contains "Exercise")
            int headerRowNumber = 3; // default fallback
            bool headerFound = false;

            for (int r = 1; r <= 10; r++)
            {
                var colA = worksheet.Cell(r, 1).GetString()?.Trim() ?? string.Empty;
                var colE = worksheet.Cell(r, 5).GetString()?.Trim() ?? string.Empty;

                if (colA.Contains("Module", StringComparison.OrdinalIgnoreCase) &&
                    colE.Contains("Exercise", StringComparison.OrdinalIgnoreCase))
                {
                    headerRowNumber = r;
                    headerFound = true;
                    break;
                }
            }

            sheetModel.HeaderRowNumber = headerRowNumber;
            if (!headerFound)
            {
                sheetModel.ValidationErrors.Add($"Header row not definitively identified in first 10 rows. Using row {headerRowNumber}.");
            }

            int lastRowUsed = worksheet.LastRowUsed()?.RowNumber() ?? headerRowNumber;
            string detectedModuleName = string.Empty;

            for (int r = headerRowNumber + 1; r <= lastRowUsed; r++)
            {
                var row = worksheet.Row(r);

                // Column E is Exercise Name
                string exerciseName = row.Cell(5).GetString()?.Trim() ?? string.Empty;

                // Section divider rows or empty rows have empty Exercise Name -> skip
                if (string.IsNullOrWhiteSpace(exerciseName))
                {
                    continue;
                }

                string moduleCol = row.Cell(1).GetString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(moduleCol) && string.IsNullOrEmpty(detectedModuleName))
                {
                    detectedModuleName = moduleCol;
                }

                string pillar = row.Cell(2).GetString()?.Trim() ?? string.Empty;
                string rawTags = row.Cell(3).GetString()?.Trim() ?? string.Empty;
                string mediaType = row.Cell(4).GetString()?.Trim() ?? "None";
                string exerciseDescription = row.Cell(6).GetString()?.Trim() ?? string.Empty;
                string completionDirections = row.Cell(7).GetString()?.Trim() ?? string.Empty;
                string completionType = row.Cell(8).GetString()?.Trim() ?? string.Empty;
                string secCompletionType = row.Cell(9).GetString()?.Trim() ?? string.Empty;
                string secPrompt = row.Cell(10).GetString()?.Trim() ?? string.Empty;

                var exercise = new BulkImportExercise
                {
                    RowNumber = r,
                    SheetName = sheetModel.SheetName,
                    ModuleName = !string.IsNullOrWhiteSpace(moduleCol) ? moduleCol : sheetModel.SheetName,
                    Pillar = pillar,
                    RawTags = rawTags,
                    Tags = SplitTags(rawTags),
                    MediaType = NormalizeMediaType(mediaType),
                    ExerciseName = exerciseName,
                    ExerciseDescription = exerciseDescription,
                    CompletionActionDirections = completionDirections,
                    RawCompletionType = completionType,
                    MappedResponseType = MapCompletionType(completionType),
                    RawSecondaryCompletionType = secCompletionType,
                    RawSecondaryPrompt = secPrompt
                };

                // Flag media requirement
                if (exercise.MediaType.Equals("Image", StringComparison.OrdinalIgnoreCase) ||
                    exercise.MediaType.Equals("GIF", StringComparison.OrdinalIgnoreCase) ||
                    exercise.MediaType.Equals("Video", StringComparison.OrdinalIgnoreCase) ||
                    completionType.Equals("Photo / Video", StringComparison.OrdinalIgnoreCase))
                {
                    exercise.IsFlaggedForMedia = true;
                    exercise.Warnings.Add($"Media type is '{exercise.MediaType}'. Media asset must be uploaded separately.");
                }

                // Construct Primary Question
                exercise.PrimaryQuestion = new BulkImportQuestion
                {
                    Id = Guid.NewGuid().ToString(),
                    QuestionText = completionDirections,
                    ResponseType = exercise.MappedResponseType
                };

                if (exercise.MappedResponseType == "Likert Scale")
                {
                    exercise.PrimaryQuestion.LikertScalePreset = "Satisfaction";
                    exercise.PrimaryQuestion.LikertLeftLabel = "Very dissatisfied";
                    exercise.PrimaryQuestion.LikertRightLabel = "Very satisfied";
                }

                // Construct Secondary Question if applicable
                if (secCompletionType.Equals("Written response", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(secPrompt))
                {
                    exercise.SecondaryQuestion = new BulkImportQuestion
                    {
                        Id = Guid.NewGuid().ToString(),
                        QuestionText = secPrompt,
                        ResponseType = "Text"
                    };
                }

                // Validate exercise data
                ValidateExercise(exercise);

                sheetModel.Exercises.Add(exercise);
            }

            sheetModel.ModuleName = !string.IsNullOrWhiteSpace(detectedModuleName) ? detectedModuleName : sheetModel.SheetName;
        }

        public static List<string> SplitTags(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new List<string>();

            // Delimiters: comma (,) or middot (·, \u00B7, \u2022)
            var parts = raw.Split(new[] { ',', '·', '•', '\u00B7', '\u2022' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>();

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !result.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(trimmed);
                }
            }

            return result;
        }

        public static string MapCompletionType(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "Unknown";

            var normalized = raw.Trim();
            if (normalized.Equals("Timer", StringComparison.OrdinalIgnoreCase))
                return "Timer";
            if (normalized.Equals("Written response", StringComparison.OrdinalIgnoreCase))
                return "Text";
            if (normalized.Equals("Yes / No", StringComparison.OrdinalIgnoreCase) || normalized.Equals("Yes/No", StringComparison.OrdinalIgnoreCase))
                return "Y/N response";
            if (normalized.Equals("Likert 1-5", StringComparison.OrdinalIgnoreCase) || normalized.Equals("Likert", StringComparison.OrdinalIgnoreCase))
                return "Likert Scale";
            if (normalized.Equals("Photo / Video", StringComparison.OrdinalIgnoreCase) || normalized.Equals("Photo/Video", StringComparison.OrdinalIgnoreCase))
                return "NoResponse";

            return normalized; // Keep as-is if already a valid ResponseType or flag for review
        }

        private static string NormalizeMediaType(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "None";

            var trimmed = raw.Trim();
            if (trimmed.Contains("video", StringComparison.OrdinalIgnoreCase)) return "Video";
            if (trimmed.Contains("gif", StringComparison.OrdinalIgnoreCase)) return "GIF";
            if (trimmed.Contains("image", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("photo", StringComparison.OrdinalIgnoreCase)) return "Image";
            if (trimmed.Contains("none", StringComparison.OrdinalIgnoreCase)) return "None";

            return trimmed;
        }

        private static void ValidateExercise(BulkImportExercise exercise)
        {
            if (string.IsNullOrWhiteSpace(exercise.ExerciseName))
                exercise.ValidationErrors.Add("Exercise Name is required.");

            if (exercise.MappedResponseType == "Unknown")
                exercise.ValidationErrors.Add($"Unrecognized Completion Type: '{exercise.RawCompletionType}'.");

            if (string.IsNullOrWhiteSpace(exercise.CompletionActionDirections))
                exercise.Warnings.Add("Completion Action Directions (Question text) is empty.");

            if (string.IsNullOrWhiteSpace(exercise.Pillar))
                exercise.Warnings.Add("Pillar is empty.");
        }

        #endregion

        #region Payload Generation

        public static Dictionary<string, object?> BuildExercisePayload(BulkImportExercise exercise, int moduleId)
        {
            var questionsList = new List<BulkImportQuestion>
            {
                exercise.PrimaryQuestion
            };

            if (exercise.SecondaryQuestion != null)
            {
                questionsList.Add(exercise.SecondaryQuestion);
            }

            // Top-level ResponseType quirk:
            // If primary is Likert Scale -> "Likert Scale,Satisfaction,Very dissatisfied,Very satisfied"
            // Otherwise -> clean ResponseType string
            string topLevelResponseType;
            if (exercise.PrimaryQuestion.ResponseType == "Likert Scale")
            {
                topLevelResponseType = "Likert Scale,Satisfaction,Very dissatisfied,Very satisfied";
            }
            else
            {
                topLevelResponseType = exercise.PrimaryQuestion.ResponseType;
            }

            // Top-level QuestionText:
            // If 1 question -> string of primary question text
            // If 2 questions -> serialized JSON string of Questions array
            string topLevelQuestionText;
            if (questionsList.Count > 1)
            {
                topLevelQuestionText = JsonSerializer.Serialize(questionsList, JsonOptions);
            }
            else
            {
                topLevelQuestionText = exercise.PrimaryQuestion.QuestionText ?? string.Empty;
            }

            var timingConfig = new Dictionary<string, object?>
            {
                { "SendTiming", new Dictionary<string, object?>
                    {
                        { "Type", "Relative" },
                        { "Relative", new Dictionary<string, object?>
                            {
                                { "Value", 1 },
                                { "Unit", "Hour" },
                                { "Reference", "LastActivity" }
                            }
                        },
                        { "Specific", null }
                    }
                },
                { "CompletionRules", new Dictionary<string, object?>
                    {
                        { "Mode", "Once" },
                        { "Days", Array.Empty<object>() }
                    }
                },
                { "ExpirationSettings", new Dictionary<string, object?>
                    {
                        { "HasExpiry", false },
                        { "ExpirationDate", null },
                        { "MoveToArchive", false },
                        { "BlockReflections", false }
                    }
                }
            };

            var contentMeasure = new Dictionary<string, object?>
            {
                { "Tags", exercise.Tags ?? new List<string>() },
                { "Pillar", exercise.Pillar ?? string.Empty }
            };

            return new Dictionary<string, object?>
            {
                { "AutoId", 0 },
                { "Id", null },
                { "ResponseType", topLevelResponseType },
                { "WidgetTitle", exercise.ExerciseName },
                { "WidgetMessage", exercise.ExerciseDescription ?? string.Empty },
                { "QuestionText", topLevelQuestionText },
                { "Duration", null },
                { "Questions", questionsList },
                { "CompletionReward", string.Empty },
                { "TimingInfo", "1 hour after last activity" },
                { "TimingConfiguration", timingConfig },
                { "BrainGoal", null },
                { "ContentMeasure", contentMeasure },
                { "TimerMeasure", "00:00" },
                { "YesCount", null },
                { "NoCount", null },
                { "SentCount", null },
                { "ResponseCount", null },
                { "UploadedImage", null },
                { "ImagePreviewUrl", null },
                { "UploadedImageBase64", null },
                { "VideoPreviewUrl", null },
                { "VideoUrl", null },
                { "IsActive", exercise.IsActive },
                { "MoveToArchive", false },
                { "DisableReflections", false },
                { "ExpirationDate", null },
                { "RepeatMode", "Once" },
                { "Reward", 0 },
                { "ModuleId", moduleId },
                { "messageNotification", Array.Empty<object>() },
                { "WhentoShow", null }
            };
        }

        #endregion

        #region Import Execution

        public void CancelImport()
        {
            _cts?.Cancel();
            IsImportRunning = false;
            IsPaused = false;
            AddLog(ImportLogLevel.Warning, "Import cancelled by user.");
            NotifyProgress();
        }

        public void TogglePause()
        {
            IsPaused = !IsPaused;
            AddLog(ImportLogLevel.Info, IsPaused ? "Import paused." : "Import resumed.");
            NotifyProgress();
        }

        public void ClearLogs()
        {
            Logs.Clear();
            NotifyProgress();
        }

        public async Task RunImportAsync(BulkImportWorkbook workbook, int museumId)
        {
            if (IsImportRunning) return;

            IsImportRunning = true;
            IsPaused = false;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            AddLog(ImportLogLevel.Info, $"🚀 Starting Bulk Import for '{workbook.FileName}' (Museum ID: {museumId}) - {workbook.Sheets.Count(s => s.IsSelected)} selected sheets.");
            NotifyProgress();

            try
            {
                foreach (var sheet in workbook.Sheets.Where(s => s.IsSelected))
                {
                    if (token.IsCancellationRequested) break;

                    while (IsPaused)
                    {
                        await Task.Delay(500, token);
                    }

                    await ImportSheetAsync(sheet, museumId, token);
                }

                int totalCreated = workbook.Sheets.Where(s => s.IsSelected).Sum(s => s.SucceededExercises);
                int totalFailed = workbook.Sheets.Where(s => s.IsSelected).Sum(s => s.FailedExercises);

                AddLog(ImportLogLevel.Success, $"🎉 Bulk Import Completed! Created: {totalCreated} exercises across modules. Failed: {totalFailed}.");
            }
            catch (OperationCanceledException)
            {
                AddLog(ImportLogLevel.Warning, "Import operation was cancelled.");
            }
            catch (Exception ex)
            {
                AddLog(ImportLogLevel.Error, $"Import process encountered an error: {ex.Message}");
                _logger.LogError(ex, "Unhandled error during bulk import");
            }
            finally
            {
                IsImportRunning = false;
                IsPaused = false;
                NotifyProgress();
            }
        }

        public async Task<bool> CreateModuleOnlyAsync(BulkImportSheet sheet, int museumId, CancellationToken token = default)
        {
            sheet.Status = ImportStatus.InProgress;
            NotifyProgress();

            var moduleDto = new ModuleCreateRequestDto
            {
                moduleId = 0,
                MuseumId = museumId,
                Name = sheet.ModuleName,
                UseMembyInput = false,
                CreateChatFeed = false,
                ChatGroupName = string.Empty,
                LinkQuiz = string.Empty,
                AgeIds = new List<int>(),
                ZoneIds = new List<int>(),
                SelectedKeys = Array.Empty<object>()
            };

            try
            {
                var endpoint = $"{ApiBase}/CreateModule";
                var reqJson = JsonSerializer.Serialize(moduleDto, JsonOptions);
                AddLog(ImportLogLevel.Request, $"POST {endpoint}\n{reqJson}", sheet.SheetName);

                var response = await _http.PostAsJsonAsync(endpoint, moduleDto, JsonOptions, token);
                var responseBody = await response.Content.ReadAsStringAsync(token);
                AddLog(ImportLogLevel.Response, $"[Status {(int)response.StatusCode}] {responseBody}", sheet.SheetName);

                if (response.IsSuccessStatusCode)
                {
                    int moduleId = ExtractModuleId(responseBody);
                    if (moduleId > 0)
                    {
                        sheet.CreatedModuleId = moduleId;
                        sheet.ErrorMessage = null;
                        AddLog(ImportLogLevel.Success, $"✅ Module '{sheet.ModuleName}' created successfully with ModuleId: {moduleId}", sheet.SheetName);
                        NotifyProgress();
                        return true;
                    }
                    else
                    {
                        sheet.Status = ImportStatus.Failed;
                        sheet.ErrorMessage = "Failed to extract returned Module ID from response.";
                        AddLog(ImportLogLevel.Error, sheet.ErrorMessage, sheet.SheetName);
                        NotifyProgress();
                        return false;
                    }
                }
                else
                {
                    sheet.Status = ImportStatus.Failed;
                    sheet.ErrorMessage = ExtractErrorMessage(responseBody, (int)response.StatusCode);
                    AddLog(ImportLogLevel.Error, $"❌ Failed to create module '{sheet.ModuleName}': {sheet.ErrorMessage}", sheet.SheetName);
                    NotifyProgress();
                    return false;
                }
            }
            catch (Exception ex)
            {
                sheet.Status = ImportStatus.Failed;
                sheet.ErrorMessage = ex.Message;
                AddLog(ImportLogLevel.Error, $"❌ Error creating module '{sheet.ModuleName}': {ex.Message}", sheet.SheetName);
                NotifyProgress();
                return false;
            }
        }

        public async Task<bool> ImportSheetAsync(BulkImportSheet sheet, int museumId, CancellationToken token = default)
        {
            sheet.Status = ImportStatus.InProgress;
            NotifyProgress();

            AddLog(ImportLogLevel.Info, $"📂 [STEP 1/2] Creating Module '{sheet.ModuleName}' for sheet '{sheet.SheetName}'...", sheet.SheetName);

            // Step 1: Create Module if not already created
            if (sheet.CreatedModuleId == null || sheet.CreatedModuleId <= 0)
            {
                bool moduleOk = await CreateModuleOnlyAsync(sheet, museumId, token);
                if (!moduleOk || sheet.CreatedModuleId == null)
                {
                    return false;
                }
            }

            int moduleId = sheet.CreatedModuleId.Value;

            // Step 2: Create Exercises under this ModuleId
            AddLog(ImportLogLevel.Info, $"📝 [STEP 2/2] Creating {sheet.Exercises.Count} exercises for module '{sheet.ModuleName}' (ID: {moduleId})...", sheet.SheetName);

            foreach (var exercise in sheet.Exercises)
            {
                if (token.IsCancellationRequested) break;

                while (IsPaused)
                {
                    await Task.Delay(500, token);
                }

                await ImportExerciseAsync(exercise, moduleId, token);
            }

            if (sheet.Exercises.All(e => e.Status == ImportStatus.Success))
            {
                sheet.Status = ImportStatus.Success;
                sheet.ErrorMessage = null;
            }
            else if (sheet.Exercises.Any(e => e.Status == ImportStatus.Success))
            {
                sheet.Status = ImportStatus.Success; // partial success
            }
            else
            {
                sheet.Status = ImportStatus.Failed;
            }

            NotifyProgress();
            return sheet.Status == ImportStatus.Success;
        }

        public async Task<bool> ImportExerciseAsync(BulkImportExercise exercise, int moduleId, CancellationToken token = default)
        {
            exercise.Status = ImportStatus.InProgress;
            NotifyProgress();

            try
            {
                var payload = BuildExercisePayload(exercise, moduleId);
                var endpoint = $"{ApiBase}/CreateMessage";
                var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
                exercise.LastRequestJson = payloadJson;

                AddLog(ImportLogLevel.Request, $"POST {endpoint} (Row #{exercise.RowNumber} - Exercise: '{exercise.ExerciseName}')\n{payloadJson}", exercise.SheetName, exercise.ExerciseName);

                var response = await _http.PostAsJsonAsync(endpoint, payload, JsonOptions, token);
                var body = await response.Content.ReadAsStringAsync(token);
                exercise.LastResponseJson = body;

                AddLog(ImportLogLevel.Response, $"[Status {(int)response.StatusCode}] {body}", exercise.SheetName, exercise.ExerciseName);

                if (response.IsSuccessStatusCode)
                {
                    exercise.Status = ImportStatus.Success;
                    exercise.ErrorMessage = null;
                    exercise.CreatedAutoId = ExtractAutoId(body);
                    AddLog(ImportLogLevel.Success, $"✅ Exercise '{exercise.ExerciseName}' created successfully! (AutoId: {exercise.CreatedAutoId})", exercise.SheetName, exercise.ExerciseName);
                    NotifyProgress();
                    return true;
                }
                else
                {
                    exercise.Status = ImportStatus.Failed;
                    exercise.ErrorMessage = ExtractErrorMessage(body, (int)response.StatusCode);
                    AddLog(ImportLogLevel.Error, $"❌ Failed to create exercise (Row #{exercise.RowNumber} '{exercise.ExerciseName}'): {exercise.ErrorMessage}", exercise.SheetName, exercise.ExerciseName);
                    NotifyProgress();
                    return false;
                }
            }
            catch (Exception ex)
            {
                exercise.Status = ImportStatus.Failed;
                exercise.ErrorMessage = ex.Message;
                AddLog(ImportLogLevel.Error, $"❌ Error creating exercise (Row #{exercise.RowNumber} '{exercise.ExerciseName}'): {ex.Message}", exercise.SheetName, exercise.ExerciseName);
                NotifyProgress();
                return false;
            }
        }

        public async Task<bool> RetrySingleExerciseAsync(BulkImportExercise exercise, BulkImportSheet sheet, int museumId, CancellationToken token = default)
        {
            if (sheet.CreatedModuleId == null || sheet.CreatedModuleId <= 0)
            {
                AddLog(ImportLogLevel.Info, $"Module '{sheet.ModuleName}' not yet created. Creating module first...", sheet.SheetName, exercise.ExerciseName);
                bool moduleOk = await CreateModuleOnlyAsync(sheet, museumId, token);
                if (!moduleOk || sheet.CreatedModuleId == null)
                {
                    AddLog(ImportLogLevel.Error, $"Cannot import exercise '{exercise.ExerciseName}' because module creation failed.", sheet.SheetName, exercise.ExerciseName);
                    return false;
                }
            }

            return await ImportExerciseAsync(exercise, sheet.CreatedModuleId.Value, token);
        }

        public async Task RunRetryFailedAsync(BulkImportWorkbook workbook, int museumId)
        {
            if (IsImportRunning) return;

            IsImportRunning = true;
            IsPaused = false;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var failedSheets = workbook.Sheets.Where(s => s.Status == ImportStatus.Failed || s.Exercises.Any(e => e.Status == ImportStatus.Failed)).ToList();
            int totalFailedExercises = failedSheets.Sum(s => s.FailedExercises);

            AddLog(ImportLogLevel.Info, $"🔄 Starting Retry for failed items: {totalFailedExercises} failed exercises across {failedSheets.Count} modules (Museum ID: {museumId})...");
            NotifyProgress();

            try
            {
                foreach (var sheet in failedSheets)
                {
                    if (token.IsCancellationRequested) break;

                    while (IsPaused)
                    {
                        await Task.Delay(500, token);
                    }

                    // If module was never created, run full sheet import
                    if (sheet.CreatedModuleId == null || sheet.CreatedModuleId <= 0)
                    {
                        await ImportSheetAsync(sheet, museumId, token);
                    }
                    else
                    {
                        // Module already exists, only retry failed exercises
                        int moduleId = sheet.CreatedModuleId.Value;
                        var exercisesToRetry = sheet.Exercises.Where(e => e.Status == ImportStatus.Failed).ToList();
                        AddLog(ImportLogLevel.Info, $"🔄 Retrying {exercisesToRetry.Count} failed exercises in module '{sheet.ModuleName}' (ID: {moduleId})...", sheet.SheetName);

                        foreach (var ex in exercisesToRetry)
                        {
                            if (token.IsCancellationRequested) break;
                            while (IsPaused) await Task.Delay(500, token);

                            await ImportExerciseAsync(ex, moduleId, token);
                        }

                        if (sheet.Exercises.All(e => e.Status == ImportStatus.Success))
                        {
                            sheet.Status = ImportStatus.Success;
                            sheet.ErrorMessage = null;
                        }
                        else if (sheet.Exercises.Any(e => e.Status == ImportStatus.Success))
                        {
                            sheet.Status = ImportStatus.Success; // partial
                        }
                        NotifyProgress();
                    }
                }

                int remainingFailed = workbook.TotalFailedExercises;
                AddLog(ImportLogLevel.Success, $"🎉 Retry Completed! Succeeded: {workbook.TotalSucceededExercises}, Remaining Failed: {remainingFailed}.");
            }
            catch (OperationCanceledException)
            {
                AddLog(ImportLogLevel.Warning, "Retry operation was cancelled.");
            }
            catch (Exception ex)
            {
                AddLog(ImportLogLevel.Error, $"Retry process encountered an error: {ex.Message}");
                _logger.LogError(ex, "Unhandled error during retry");
            }
            finally
            {
                IsImportRunning = false;
                IsPaused = false;
                NotifyProgress();
            }
        }

        public static string ExtractErrorMessage(string responseBody, int statusCode)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return $"HTTP {statusCode} (Empty response)";

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                // 1. check "message" or "Message"
                if (root.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(msgProp.GetString()))
                    return msgProp.GetString()!;
                if (root.TryGetProperty("Message", out var msgPropUpper) && msgPropUpper.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(msgPropUpper.GetString()))
                    return msgPropUpper.GetString()!;

                // 2. check "error" or "Error"
                if (root.TryGetProperty("error", out var errProp) && errProp.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(errProp.GetString()))
                    return errProp.GetString()!;

                // 3. check ASP.NET ValidationProblemDetails "errors"
                if (root.TryGetProperty("errors", out var errorsProp) && errorsProp.ValueKind == JsonValueKind.Object)
                {
                    var errList = new List<string>();
                    foreach (var prop in errorsProp.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            var msgs = prop.Value.EnumerateArray().Select(v => v.GetString()).Where(s => !string.IsNullOrEmpty(s));
                            errList.Add($"{prop.Name}: {string.Join(", ", msgs)}");
                        }
                        else if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            errList.Add($"{prop.Name}: {prop.Value.GetString()}");
                        }
                    }
                    if (errList.Any())
                    {
                        return string.Join("; ", errList);
                    }
                }

                if (root.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(titleProp.GetString()))
                    return titleProp.GetString()!;
            }
            catch { }

            var snippet = responseBody.Length > 200 ? responseBody.Substring(0, 200) + "..." : responseBody;
            return $"HTTP {statusCode}: {snippet}";
        }

        private static int ExtractModuleId(string jsonResponse)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var dataElem))
                {
                    if (dataElem.ValueKind == JsonValueKind.Number && dataElem.TryGetInt32(out var idNum))
                        return idNum;

                    if (dataElem.ValueKind == JsonValueKind.Object)
                    {
                        if (dataElem.TryGetProperty("modulePayloadId", out var mpId) && mpId.TryGetInt32(out var id1))
                            return id1;
                        if (dataElem.TryGetProperty("moduleId", out var mId) && mId.TryGetInt32(out var id2))
                            return id2;
                        if (dataElem.TryGetProperty("id", out var idElem) && idElem.TryGetInt32(out var id3))
                            return id3;
                    }
                }

                if (root.TryGetProperty("modulePayloadId", out var rootMpId) && rootMpId.TryGetInt32(out var rId1))
                    return rId1;
                if (root.TryGetProperty("moduleId", out var rootMId) && rootMId.TryGetInt32(out var rId2))
                    return rId2;
            }
            catch { }

            return 0;
        }

        private static int ExtractAutoId(string jsonResponse)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                if (root.TryGetProperty("autoId", out var autoIdElem) && autoIdElem.TryGetInt32(out var aId))
                    return aId;

                if (root.TryGetProperty("data", out var dataElem))
                {
                    if (dataElem.ValueKind == JsonValueKind.Number && dataElem.TryGetInt32(out var num))
                        return num;
                    if (dataElem.ValueKind == JsonValueKind.Object && dataElem.TryGetProperty("autoId", out var dAutoId) && dAutoId.TryGetInt32(out var daId))
                        return daId;
                }
            }
            catch { }

            return 0;
        }

        private void AddLog(ImportLogLevel level, string message, string sheetName = "", string? exerciseName = null, string? payloadSnippet = null)
        {
            var log = new ImportLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                SheetName = sheetName,
                ExerciseName = exerciseName,
                Message = message,
                PayloadSnippet = payloadSnippet
            };

            lock (Logs)
            {
                Logs.Add(log);
                if (Logs.Count > 1000)
                {
                    Logs.RemoveAt(0);
                }
            }

            OnLogAdded?.Invoke(log);
        }

        private void NotifyProgress()
        {
            OnProgressChanged?.Invoke();
        }

        #endregion
    }
}
