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

    /// <summary>Tạo user Teacher mới hoặc nâng Student đã có (cùng email, đúng mật khẩu).</summary>
    Task<(bool Success, string ErrorMessage, AppUser? User)> PrepareTeacherUserAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task EnsureSeedUsersAsync(CancellationToken cancellationToken = default);
}

