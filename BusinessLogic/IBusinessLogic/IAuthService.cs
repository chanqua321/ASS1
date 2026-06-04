using Model.Entities;

namespace BusinessLogic.IBusinessLogic;

public interface IAuthService
{
    Task<(bool Success, string ErrorMessage, int UserId, string Username, string Role)> ValidateLoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string ErrorMessage)> RegisterStudentAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>Tạo tài khoản Teacher mới hoặc dùng Teacher đã có (gán thêm môn).</summary>
    Task<(bool Success, string ErrorMessage, AppUser? User)> PrepareTeacherUserAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);
}

