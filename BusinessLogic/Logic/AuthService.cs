using BusinessLogic.Helpers;
using BusinessLogic.IBusinessLogic;
using Model.Entities;
using Model.IRepository;
using Model.IUnitOfWork;

namespace BusinessLogic.Logic;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;

    public AuthService(IUserRepository users, IUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    public async Task EnsureSeedUsersAsync(CancellationToken cancellationToken = default)
    {
        // Seed account tối thiểu để tránh "không đăng nhập được" khi DB mới.
        // Đồng thời dọn demo teacher/student (chỉ xoá nếu đúng dạng demo: username cố định + mật khẩu "123").
        //
        // Lý do: yêu cầu "xoá dữ liệu demo teacher" nhưng vẫn cần admin để quản trị.

        var changed = false;

        // Seed admin mặc định dùng email (theo yêu cầu login Gmail/email)
        if (!await _users.EmailExistsAsync("admin@gmail.com", cancellationToken))
        {
            await _users.AddAsync(new AppUser
            {
                Email = "admin@gmail.com",
                PasswordHash = PasswordHashHelper.Hash("123"),
                Role = "Admin"
            }, cancellationToken);
            changed = true;
        }

        // Cleanup demo users nếu còn tồn tại (xác định bằng password demo "123").
        // Không xoá nếu user đã đổi mật khẩu (tránh xoá nhầm).
        changed |= await DeleteDemoUserIfMatchesAsync("teacher", "123", cancellationToken);
        changed |= await DeleteDemoUserIfMatchesAsync("student", "123", cancellationToken);

        if (changed)
            await _uow.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> DeleteDemoUserIfMatchesAsync(
        string email,
        string demoPassword,
        CancellationToken cancellationToken)
    {
        var user = await _users.FindByEmailAsync(email, cancellationToken);
        if (user is null) return false;

        // Chỉ xoá nếu đúng password demo
        if (!PasswordHashHelper.Verify(demoPassword, user.PasswordHash)) return false;

        _users.Remove(user);
        return true;
    }

    public async Task<(bool Success, string ErrorMessage, int UserId, string Username, string Role)> ValidateLoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        username = (username ?? string.Empty).Trim();
        if (username.Length == 0)
            return (false, "Thiếu tài khoản.", 0, "", "");

        var user = await _users.FindByEmailAsync(username, cancellationToken);
        if (user is null || !PasswordHashHelper.Verify(password, user.PasswordHash))
            return (false, "Sai tài khoản hoặc mật khẩu.", 0, "", "");

        return (true, "", user.Id, user.Email, user.Role);
    }

    public async Task<(bool Success, string ErrorMessage)> RegisterStudentAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        username = (username ?? string.Empty).Trim();
        if (username.Length < 3)
            return (false, "Tài khoản tối thiểu 3 ký tự.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 3)
            return (false, "Mật khẩu tối thiểu 3 ký tự.");

        if (await _users.EmailExistsAsync(username, cancellationToken))
            return (false, "Tài khoản đã tồn tại.");

        await _users.AddAsync(new AppUser
        {
            Email = username,
            PasswordHash = PasswordHashHelper.Hash(password),
            Role = "Student"
        }, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);
        return (true, "");
    }

    public async Task<(bool Success, string ErrorMessage, AppUser? User)> PrepareTeacherUserAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        email = (email ?? string.Empty).Trim();
        if (email.Length < 3)
            return (false, "Email tối thiểu 3 ký tự.", null);

        var existing = await _users.FindByEmailIgnoreCaseAsync(email, cancellationToken);
        if (existing is not null)
        {
            // Teacher đã tồn tại → Admin có thể gán thêm môn khác cho cùng email (không cần mật khẩu).
            if (existing.Role == "Teacher")
                return (true, "", existing);
            if (existing.Role == "Admin")
                return (false, "Không thể dùng email Admin làm Teacher.", null);

            if (string.IsNullOrWhiteSpace(password) || password.Length < 3)
                return (false, "Mật khẩu tối thiểu 3 ký tự.", null);

            if (!PasswordHashHelper.Verify(password, existing.PasswordHash))
                return (false, "Email đã đăng ký (Student). Nhập đúng mật khẩu hiện tại hoặc dùng email khác.", null);

            existing.Role = "Teacher";
            await _uow.SaveChangesAsync(cancellationToken);
            return (true, "", existing);
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 3)
            return (false, "Mật khẩu tối thiểu 3 ký tự.", null);

        var (ok, err) = await RegisterStudentAsync(email, password, cancellationToken);
        if (!ok)
            return (false, err, null);

        var created = await _users.FindByEmailIgnoreCaseAsync(email, cancellationToken);
        if (created is null)
            return (false, "Không tìm thấy tài khoản vừa tạo.", null);

        created.Role = "Teacher";
        await _uow.SaveChangesAsync(cancellationToken);
        return (true, "", created);
    }
}

