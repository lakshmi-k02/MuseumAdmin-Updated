using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace MuseumAdmin.Models
{
    public class BulkImportWorkbook
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public DateTime LoadedAt { get; set; } = DateTime.UtcNow;
        public List<BulkImportSheet> Sheets { get; set; } = new();

        public int TotalModules => Sheets.Count;
        public int TotalExercises => Sheets.Sum(s => s.Exercises.Count);
        public int TotalWithSecondaryQuestions => Sheets.Sum(s => s.Exercises.Count(e => e.SecondaryQuestion != null));
        public int TotalFlaggedForMedia => Sheets.Sum(s => s.Exercises.Count(e => e.IsFlaggedForMedia));
        public int TotalValidationErrors => Sheets.Sum(s => s.ValidationErrors.Count + s.Exercises.Sum(e => e.ValidationErrors.Count));
        public int TotalActiveExercises => Sheets.Sum(s => s.Exercises.Count(e => e.IsActive));
        public int TotalDraftExercises => Sheets.Sum(s => s.Exercises.Count(e => !e.IsActive));
        public int TotalSucceededExercises => Sheets.Sum(s => s.SucceededExercises);
        public int TotalFailedExercises => Sheets.Sum(s => s.FailedExercises);
        public int TotalFailedSheets => Sheets.Count(s => s.Status == ImportStatus.Failed);
        public bool HasFailures => TotalFailedExercises > 0 || TotalFailedSheets > 0 || TotalValidationErrors > 0;

        public List<FailedImportItem> GetFailedItems()
        {
            var items = new List<FailedImportItem>();

            foreach (var sheet in Sheets)
            {
                // If the entire sheet failed at Module creation (Step 1)
                if (sheet.Status == ImportStatus.Failed && sheet.CreatedModuleId == null)
                {
                    // If no exercises were processed yet because module creation failed
                    if (!sheet.Exercises.Any(e => e.Status == ImportStatus.Failed))
                    {
                        foreach (var ex in sheet.Exercises)
                        {
                            items.Add(new FailedImportItem
                            {
                                SheetName = sheet.SheetName,
                                ModuleName = sheet.ModuleName,
                                ModuleId = sheet.CreatedModuleId,
                                RowNumber = ex.RowNumber,
                                ExerciseName = ex.ExerciseName,
                                ExerciseDescription = ex.ExerciseDescription,
                                Pillar = ex.Pillar,
                                MappedResponseType = ex.MappedResponseType,
                                CompletionDirections = ex.CompletionActionDirections,
                                SecondaryPrompt = ex.RawSecondaryPrompt,
                                Tags = ex.Tags,
                                FailureType = ImportFailureType.ModuleCreationFailed,
                                ErrorReason = !string.IsNullOrWhiteSpace(sheet.ErrorMessage) ? sheet.ErrorMessage : "Module creation failed during Step 1.",
                                RawResponse = ex.LastResponseJson,
                                RawRequest = ex.LastRequestJson,
                                ValidationErrors = ex.ValidationErrors,
                                Warnings = ex.Warnings,
                                ExerciseRef = ex,
                                SheetRef = sheet
                            });
                        }
                        continue;
                    }
                }

                // Check individual failed exercises or exercises with validation errors
                foreach (var ex in sheet.Exercises)
                {
                    if (ex.Status == ImportStatus.Failed || ex.ValidationErrors.Any())
                    {
                        string reason;
                        if (ex.ValidationErrors.Any())
                        {
                            reason = string.Join("; ", ex.ValidationErrors);
                        }
                        else if (!string.IsNullOrWhiteSpace(ex.ErrorMessage))
                        {
                            reason = ex.ErrorMessage;
                        }
                        else
                        {
                            reason = "API returned an unsuccessful status or error response.";
                        }

                        items.Add(new FailedImportItem
                        {
                            SheetName = sheet.SheetName,
                            ModuleName = sheet.ModuleName,
                            ModuleId = sheet.CreatedModuleId,
                            RowNumber = ex.RowNumber,
                            ExerciseName = ex.ExerciseName,
                            ExerciseDescription = ex.ExerciseDescription,
                            Pillar = ex.Pillar,
                            MappedResponseType = ex.MappedResponseType,
                            CompletionDirections = ex.CompletionActionDirections,
                            SecondaryPrompt = ex.RawSecondaryPrompt,
                            Tags = ex.Tags,
                            FailureType = ex.ValidationErrors.Any() ? ImportFailureType.ValidationError : ImportFailureType.ExerciseCreationFailed,
                            ErrorReason = reason,
                            RawResponse = ex.LastResponseJson,
                            RawRequest = ex.LastRequestJson,
                            ValidationErrors = ex.ValidationErrors,
                            Warnings = ex.Warnings,
                            ExerciseRef = ex,
                            SheetRef = sheet
                        });
                    }
                }
            }

            return items;
        }
    }

    public class BulkImportSheet
    {
        public string SheetName { get; set; } = string.Empty;
        public int SheetIndex { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public string ModuleDescription { get; set; } = string.Empty;
        public int HeaderRowNumber { get; set; } = 3;
        public List<BulkImportExercise> Exercises { get; set; } = new();
        public List<string> ValidationErrors { get; set; } = new();

        // Import Execution State
        public bool IsSelected { get; set; } = true;
        public ImportStatus Status { get; set; } = ImportStatus.Pending;
        public int? CreatedModuleId { get; set; }
        public string? ErrorMessage { get; set; }
        public int SucceededExercises => Exercises.Count(e => e.Status == ImportStatus.Success);
        public int FailedExercises => Exercises.Count(e => e.Status == ImportStatus.Failed);
    }

    public class BulkImportExercise
    {
        public int RowNumber { get; set; }
        public string SheetName { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string Pillar { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public string RawTags { get; set; } = string.Empty;
        public string MediaType { get; set; } = "None"; // None, Image, GIF, Video
        public string ExerciseName { get; set; } = string.Empty;
        public string ExerciseDescription { get; set; } = string.Empty;
        public string CompletionActionDirections { get; set; } = string.Empty;
        public string RawCompletionType { get; set; } = string.Empty;
        public string MappedResponseType { get; set; } = string.Empty;
        public string RawSecondaryCompletionType { get; set; } = string.Empty;
        public string RawSecondaryPrompt { get; set; } = string.Empty;

        // Structured Questions
        public BulkImportQuestion PrimaryQuestion { get; set; } = new();
        public BulkImportQuestion? SecondaryQuestion { get; set; }

        public bool IsFlaggedForMedia { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        // Import Execution State
        public bool IsActive { get; set; } = true;
        public ImportStatus Status { get; set; } = ImportStatus.Pending;
        public int? CreatedAutoId { get; set; }
        public string? ErrorMessage { get; set; }
        public string? LastRequestJson { get; set; }
        public string? LastResponseJson { get; set; }
    }

    public class FailedImportItem
    {
        public string SheetName { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public int? ModuleId { get; set; }
        public int RowNumber { get; set; }
        public string ExerciseName { get; set; } = string.Empty;
        public string ExerciseDescription { get; set; } = string.Empty;
        public string Pillar { get; set; } = string.Empty;
        public string MappedResponseType { get; set; } = string.Empty;
        public string CompletionDirections { get; set; } = string.Empty;
        public string? SecondaryPrompt { get; set; }
        public List<string> Tags { get; set; } = new();
        public ImportFailureType FailureType { get; set; } = ImportFailureType.ExerciseCreationFailed;
        public string ErrorReason { get; set; } = string.Empty;
        public string? RawResponse { get; set; }
        public string? RawRequest { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public BulkImportExercise? ExerciseRef { get; set; }
        public BulkImportSheet? SheetRef { get; set; }
    }

    public enum ImportFailureType
    {
        ValidationError,
        ModuleCreationFailed,
        ExerciseCreationFailed,
        NetworkError
    }

    public class BulkImportQuestion
    {
        [JsonPropertyName("Id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("QuestionText")]
        public string QuestionText { get; set; } = string.Empty;

        [JsonPropertyName("ResponseType")]
        public string ResponseType { get; set; } = string.Empty;

        [JsonPropertyName("Duration")]
        public int? Duration { get; set; }

        [JsonPropertyName("LikertScalePreset")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LikertScalePreset { get; set; }

        [JsonPropertyName("LikertLeftLabel")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LikertLeftLabel { get; set; }

        [JsonPropertyName("LikertRightLabel")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LikertRightLabel { get; set; }
    }

    public enum ImportStatus
    {
        Pending,
        InProgress,
        Success,
        Failed,
        Skipped
    }

    public enum ImportLogLevel
    {
        Info,
        Request,
        Response,
        Success,
        Warning,
        Error
    }

    public class ImportLogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public ImportLogLevel Level { get; set; } = ImportLogLevel.Info;
        public string SheetName { get; set; } = string.Empty;
        public string? ExerciseName { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? PayloadSnippet { get; set; }
    }

    public class ModuleCreateRequestDto
    {
        public int moduleId { get; set; } = 0;
        public int MuseumId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool UseMembyInput { get; set; } = false;
        public bool CreateChatFeed { get; set; } = false;
        public string ChatGroupName { get; set; } = string.Empty;
        public string LinkQuiz { get; set; } = string.Empty;
        public List<int> AgeIds { get; set; } = new();
        public List<int> ZoneIds { get; set; } = new();
        public object[] SelectedKeys { get; set; } = Array.Empty<object>();
    }

    public class ModuleCreateResponseDto
    {
        public bool Status { get; set; }
        public int StatusCode { get; set; }
        public string? Message { get; set; }
        public System.Text.Json.JsonElement? Data { get; set; }
    }
}
