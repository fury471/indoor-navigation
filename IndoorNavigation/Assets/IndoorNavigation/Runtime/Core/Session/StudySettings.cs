namespace IndoorNavigation.Core.Session
{
    /// <summary>The two RQ1 conditions. Positioning is the same in both.</summary>
    public enum StudyCondition { TwoDOnly, TwoDPlusAr }

    /// <summary>
    /// Set per participant in the developer panel's Study group and written into every
    /// log line. In 2D only, raising the phone keeps the 2D map.
    /// </summary>
    public sealed class StudySettings
    {
        public string ParticipantCode { get; set; } = "dev";
        public StudyCondition Condition { get; set; } = StudyCondition.TwoDPlusAr;
    }
}
