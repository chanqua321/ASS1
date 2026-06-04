# Business Roles (RBAC) — EduAI (Assigment1)

Tài liệu này mô tả **vai trò nghiệp vụ (business roles)** trong hệ thống EduAI theo đúng code hiện tại (ASP.NET Core MVC + Cookie Auth + Role-based Authorization).

## 1) Các vai trò

Hệ thống có 3 role:

- **Admin**
  - Quản lý người dùng
  - Là người **duy nhất** được tạo tài khoản **Teacher**
  - Gán Teacher cho môn học (mỗi môn chỉ 1 teacher)
- **Teacher**
  - Upload tài liệu và lập chỉ mục (index) để phục vụ Chat
  - (Mục tiêu) chỉ thao tác trên môn mình phụ trách
- **Student**
  - Chỉ được xem / tải tài liệu và sử dụng Chat
  - **Không được thấy** và **không truy cập được** các màn Upload/Reindex

## 2) Đăng nhập / Đăng ký

- **Login**
  - URL: `GET/POST /Account/Login`
  - Controller: `Web/Controllers/AccountController.cs`
  - Khi đăng nhập thành công, hệ thống tạo cookie chứa claim:
    - `ClaimTypes.NameIdentifier` = `userId`
    - `ClaimTypes.Name` = `username`
    - `ClaimTypes.Role` = `role` (Admin/Teacher/Student)

- **Register**
  - URL: `GET/POST /Account/Register`
  - Chỉ đăng ký **Student**
  - Teacher không tự đăng ký (đúng yêu cầu nghiệp vụ)

## 3) Seed tài khoản demo (tạo tự động trong DB)

Khi app khởi động, hệ thống gọi `IAuthService.EnsureSeedUsersAsync()` để đảm bảo có các tài khoản sau (nếu chưa tồn tại):

- `admin / 123` → role **Admin**
- `teacher / 123` → role **Teacher**
- `student / 123` → role **Student**

> Lưu ý: seed chỉ nhằm demo/assignment. Thực tế có thể bỏ seed và dùng UI Admin tạo user.

## 4) Quyền truy cập theo chức năng (Web endpoints)

### 4.1 Quản lý tài liệu (WF1)

Controller: `Web/Controllers/DocumentsController.cs`

- **Xem danh sách tài liệu**
  - `GET /Documents` → `Index`
  - Quyền: **Authenticated** (Admin/Teacher/Student)

- **Xem chi tiết tài liệu + nội dung trích xuất**
  - `GET /Documents/Details/{id}` → `Details`
  - Quyền: **Authenticated**
  - Trang chi tiết hiển thị:
    - Preview text trích xuất
    - Outline (mục lớn/mục nhỏ)
    - Chunk preview

- **Tải file**
  - `GET /Documents/Download/{id}` → `Download`
  - Quyền: **Authenticated**

- **Upload tài liệu**
  - `GET /Documents/Upload` → `Upload (GET)`
  - `POST /Documents/Upload` → `Upload (POST)`
  - Quyền: **TeacherOnly** (role Teacher)
  - Student **không thấy** link/nút Upload trên UI và cũng bị chặn bởi `[Authorize(Policy="TeacherOnly")]`

- **Reindex (Index lại)**
  - `POST /Documents/Reindex/{id}` → `Reindex`
  - Quyền: **TeacherOnly**

- **Load chapter theo môn (phục vụ Upload UI)**
  - `GET /Documents/ChaptersBySubject?subjectId=...` → `ChaptersBySubject`
  - Quyền: **TeacherOnly**

### 4.2 Chat hỏi đáp (WF2)

Controller: `Web/Controllers/ChatController.cs`

- `GET /Chat` → danh sách phiên
- `POST /Chat/Create` → tạo phiên chat
- `GET /Chat/Session/{id}` → vào phiên
- `POST /Chat/Send` (JSON) → gửi câu hỏi
- `POST /Chat/Enroll` (JSON) → đăng ký môn (enrollment)
- `POST /Chat/Delete/{id}` → xóa phiên

Quyền: **Authenticated** (Admin/Teacher/Student đều chat được)

## 5) Admin quản lý user + tạo Teacher

Controller: `Web/Controllers/AdminUsersController.cs`

- `GET /AdminUsers`
  - Xem danh sách user
  - Xem danh sách môn + trạng thái đã có teacher hay chưa

- `GET/POST /AdminUsers/CreateTeacher`
  - Admin tạo 1 tài khoản mới (ban đầu register như student) → nâng role thành Teacher
  - Admin gán teacher đó vào 1 môn
  - Rule enforced: **mỗi môn chỉ có 1 teacher**

Quyền: `[Authorize(Roles="Admin")]` (chỉ Admin)

## 6) Quy tắc “mỗi môn chỉ 1 teacher”

Entity: `Model/Entities/Subject.cs`

- Thuộc tính:
  - `TeacherUserId`
  - `TeacherUser`

Ý nghĩa nghiệp vụ:
- Một môn học chỉ có **một** teacher chịu trách nhiệm cập nhật tài liệu của môn đó.
- Admin là người gán teacher cho môn.

## 7) UI/UX theo role (ẩn/hiện đúng vai trò)

Các chỗ UI đã ẩn Upload đối với Student:

- Navbar: `Web/Views/Shared/_Layout.cshtml`
  - Mục Upload chỉ hiện khi `User.IsInRole("Teacher")`
  - Mục Admin chỉ hiện khi `User.IsInRole("Admin")`

- Home page: `Web/Views/Home/Index.cshtml`
  - Button/card Upload chỉ hiện cho Teacher

- Documents list: `Web/Views/Documents/Index.cshtml`
  - Button Upload header chỉ hiện cho Teacher
  - Empty state “Upload ngay” chỉ hiện cho Teacher

## 8) Ghi chú về bảo mật

- **Ẩn nút trên UI không đủ**: hệ thống vẫn chặn backend bằng `[Authorize]`/policy.
- Role được lấy từ claim trong cookie (không tin vào client-side).
- Mật khẩu lưu dạng hash PBKDF2: `BusinessLogic/Helpers/PasswordHashHelper.cs`

