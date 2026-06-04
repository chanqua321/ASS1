using Model.Enums;

namespace BusinessLogic.Helpers;

public static class DocumentDisplayHelper
{
    public static (string BadgeClass, string Icon, string Label) GetStatusDisplay(DocumentStatus status) =>
        status switch
        {
            DocumentStatus.Indexed => ("bg-success", "bi-check-circle-fill", "Indexed"),
            DocumentStatus.Processing => ("bg-warning", "bi-hourglass-split", "Processing"),
            DocumentStatus.Failed => ("bg-danger", "bi-x-circle-fill", "Failed"),
            _ => ("bg-secondary", "bi-clock", status.ToString())
        };

    public static bool IsIndexed(DocumentStatus status) => status == DocumentStatus.Indexed;
}
