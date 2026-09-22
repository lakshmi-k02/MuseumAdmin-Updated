using System;
using System.Collections.Generic;

namespace MuseumAdmin.Models
{
    public class CheckInForm
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsAdminDefault { get; set; } = false;
        public List<CheckInQuestion> Questions { get; set; } = new();
    }

    public class CheckInQuestion
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Type { get; set; } = "ShortText"; // ShortText, LongText, YesNo, Likert, Slider, StarRating, MultipleChoice, Date
        public string Text { get; set; } = string.Empty;
        public bool IsRequired { get; set; } = false;
        public bool IncludeHintText { get; set; } = false;
        public string HintText { get; set; } = string.Empty;
        
        // Likert Specific
        public string LikertScalePreset { get; set; } = "agreement_5";
        public string LikertLeftLabel { get; set; } = "Strongly disagree";
        public string LikertRightLabel { get; set; } = "Strongly agree";
        public string LikertLabel2 { get; set; } = "Disagree";
        public string LikertLabel3 { get; set; } = "Neutral";
        public string LikertLabel4 { get; set; } = "Agree";
        
        // Slider Specific
        public int SliderMin { get; set; } = 0;
        public int SliderMax { get; set; } = 10;

        // Multiple Choice Specific
        public List<string> Options { get; set; } = new();
    }
}
