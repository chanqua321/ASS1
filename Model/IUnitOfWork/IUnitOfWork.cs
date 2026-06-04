namespace Model.IUnitOfWork;

/// Lưu thay đổi xuống DB — Service gọi, không gọi DbContext trực tiếp.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
