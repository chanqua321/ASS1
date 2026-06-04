namespace BusinessLogic.DTOs;

public class AdminDashboardDto
{
    public int TotalUsers { get; set; }
    public int TeacherCount { get; set; }
    public int StudentCount { get; set; }
    public int SubjectCount { get; set; }
    public int DocumentCount { get; set; }
    public int ChatSessionCount { get; set; }
    public int QuestionCount { get; set; }
    public IReadOnlyList<ChartSeriesPointDto> ChatByDay { get; set; } = Array.Empty<ChartSeriesPointDto>();
    public IReadOnlyList<ChartSeriesPointDto> UploadsByMonth { get; set; } = Array.Empty<ChartSeriesPointDto>();
    public IReadOnlyList<SubjectAccessDto> TopSubjects { get; set; } = Array.Empty<SubjectAccessDto>();
}

public class ChartSeriesPointDto
{
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
}

public class SubjectAccessDto
{
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public int SessionCount { get; set; }
}
