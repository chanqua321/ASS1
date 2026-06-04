# EduAI (Assigment1) — Tài liệu dự án đầy đủ

> **Phiên bản:** theo codebase hiện tại (ASP.NET Core 8 MVC, SQL Server LocalDB, RAG Chat với Ollama).  
> **Mục đích:** hướng dẫn cấu hình, kiến trúc, luồng nghiệp vụ và các điểm cần chú ý khi đọc/sửa code.

---

## Mục lục

1. [Tổng quan](#1-tổng-quan)
2. [Yêu cầu & chạy dự án](#2-yêu-cầu--chạy-dự-án)
3. [Kiến trúc solution](#3-kiến-trúc-solution)
4. [Cấu hình (`appsettings`)](#4-cấu-hình-appsettings)
5. [Khởi động ứng dụng (`Program.cs`)](#5-khởi-động-ứng-dụng-programcs)
6. [Phân quyền (RBAC)](#6-phân-quyền-rbac)
7. [Luồng nghiệp vụ WF1 — Quản lý tài liệu](#7-luồng-nghiệp-vụ-wf1--quản-lý-tài-liệu)
8. [Luồng nghiệp vụ WF2 — Chat RAG](#8-luồng-nghiệp-vụ-wf2--chat-rag)
9. [Bảng endpoint Web](#9-bảng-endpoint-web)
10. [Cơ sở dữ liệu & Entity](#10-cơ-sở-dữ-liệu--entity)
11. [Dependency Injection & Services](#11-dependency-injection--services)
12. [Tính năng bổ sung](#12-tính-năng-bổ-sung)
13. [Quy ước code & anti-pattern cần tránh](#13-quy-ước-code--anti-pattern-cần-tránh)
14. [Xử lý sự cố thường gặp](#14-xử-lý-sự-cố-thường-gặp)
15. [Cấu trúc thư mục quan trọng](#15-cấu-trúc-thư-mục-quan-trọng)
16. [Tài liệu liên quan](#16-tài-liệu-liên-quan)

---

## 1. Tổng quan

**EduAI** là ứng dụng web giáo dục hỗ trợ:

- **WF1:** Teacher upload tài liệu (PDF/DOCX/PPT), trích xuất text, chia chunk, tạo embedding, lập chỉ mục để tra cứu.
- **WF2:** Student/Teacher/Admin chat hỏi–đáp theo tài liệu đã index (RAG), có thể bật trích dẫn nguồn.
- **Quản trị:** Admin tạo môn/chương, gán giáo viên (mỗi môn một teacher; một teacher có thể nhiều môn), xem audit & dashboard.
- **Mở rộng:** Tóm tắt AI trên document, sinh quiz, export quiz.

**Stack:**

| Thành phần | Công nghệ |
|------------|-----------|
| Web UI | ASP.NET Core MVC 8, Razor |
| Auth | Cookie Authentication, Role-based Authorization |
| DB | EF Core 8 + SQL Server (LocalDB mặc định) |
| AI chat | Ollama (OpenAI-compatible API) hoặc fallback local |
| Embedding | `MockEmbeddingService` (hash-based, demo) |
| File upload | Lưu disk `App_Data/uploads` |

**Solution:** `Assigment1.sln` gồm 3 project:

- `Web` — presentation
- `BusinessLogic` — nghiệp vụ + DTO + options
- `Model` — EF entities, repositories, migrations

---

## 2. Yêu cầu & chạy dự án

### 2.1 Phần mềm cần có

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- **SQL Server LocalDB** (đi kèm Visual Studio hoặc cài riêng)
- (Khuyến nghị) **[Ollama](https://ollama.com)** + model `llama3.2` nếu muốn chat AI thật

### 2.2 Chạy từ CLI

```bash
cd Web
dotnet run
```

Hoặc mở `Assigment1.sln` trong Visual Studio, profile **https**.

**URL mặc định** (`launchSettings.json`):

| Profile | URL |
|---------|-----|
| https | `https://localhost:7288` / `http://localhost:5288` |
| http | `http://localhost:5288` |
| IIS Express | `http://localhost:16651` (SSL 44300) |

**Route mặc định:** `{controller=Documents}/{action=Index}` — sau login thường vào **Kho tài liệu**.

### 2.3 Tài khoản khởi tạo

Khi app chạy, `IAuthService.EnsureSeedUsersAsync()` đảm bảo:

| Email | Mật khẩu | Role |
|-------|----------|------|
| `admin@gmail.com` | `123` | Admin |

**Lưu ý:** Các user demo cũ `teacher` / `student` (mật khẩu `123`) sẽ bị **xóa tự động** nếu vẫn còn password demo — tránh nhầm với tài khoản thật. Teacher/Student mới do Admin tạo hoặc Student tự **Register**.

### 2.4 Ollama (tùy chọn nhưng khuyến nghị)

```bash
ollama pull llama3.2
ollama serve
```

Kiểm tra API: `http://localhost:11434/v1/` (khớp `Chat:Ai:BaseUrl` trong appsettings).

---

## 3. Kiến trúc solution

### 3.1 Sơ đồ phụ thuộc

```
┌─────────────┐
│     Web     │  Controllers, Views, ViewModels
└──────┬──────┘
       │ IBusinessLogic.*
┌──────▼──────────────┐
│   BusinessLogic     │  Services, DTOs, Helpers, Options
└──────┬──────────────┘
       │ IRepository.*, IUnitOfWork
┌──────▼──────┐
│    Model    │  AppDbContext, Entities, Repositories, Migrations
└─────────────┘
```

### 3.2 Quy tắc phân lớp

| Lớp | Được phép | Không được |
|-----|-----------|------------|
| **Web** | HTTP, ViewModel, map DTO, `[Authorize]` | Gọi `AppDbContext` trực tiếp (trừ vài chỗ legacy — xem §13) |
| **BusinessLogic** | Orchestrate nghiệp vụ, đọc/ghi qua repository | Reference View/Razor |
| **Model** | EF, SQL, entity | Logic UI hoặc gọi Ollama |

**Luồng chuẩn:**

```
HTTP Request → Controller → I*Service (BusinessLogic) → I*Repository → AppDbContext → SQL
```

### 3.3 Sơ đồ draw.io

- `docs/Assigment1-Architecture.drawio` — chi tiết
- `docs/Assigment1-Simple-Architecture.drawio` — đơn giản

---

## 4. Cấu hình (`appsettings`)

File chính: `Web/appsettings.json`  
Override dev: `Web/appsettings.Development.json` (thêm log EF Core).

### 4.1 Connection string

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=Assigment1DocDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

- **Bắt buộc** key `DefaultConnection` — thiếu → `InvalidOperationException` khi build app.
- Đổi server: sửa `Server=...` (SQL Express, Azure SQL, v.v.).
- Sau đổi DB: có thể cần `dotnet ef database update` hoặc để app tự `MigrateAsync()`.

### 4.2 Lưu trữ file

```json
"DocumentStorage": {
  "UploadPath": "App_Data/uploads"
}
```

- `Program.cs` ghép: `{ContentRoot}/{UploadPath}`.
- File vật lý: `{Guid}{extension}`; DB lưu metadata + `FilePath`.

**Class:** `BusinessLogic/Options/DocumentStorageOptions.cs`

### 4.3 RAG — `Chat:Rag`

Map vào `RagChatOptions` (`BusinessLogic/Options/RagChatOptions.cs`).

| Thuộc tính | Mặc định | Ý nghĩa |
|------------|----------|---------|
| `TopK` | 5 | Số chunk retrieve mỗi câu hỏi thường |
| `MinSimilarityScore` | 0.12 | Ngưỡng cosine similarity chính |
| `FallbackMinScore` | 0.05 | Dùng top chunk khi không đạt ngưỡng chính |
| `MetadataBoostScore` | 0.92 | Boost khi câu hỏi khớp mã/tên môn hoặc tên file |
| `MaxExcerptLength` | 280 | Độ dài excerpt trong fallback / citation |
| `MaxHistoryMessages` | 6 | Số tin nhắn history đưa vào prompt AI |
| `IncludeCitationsByDefault` | false | UI có thể bật trích dẫn từng lần gửi |
| `MaxCitations` | 3 | Số nguồn tối đa |
| `MinCitationScore` | 0.35 | Chỉ hiện excerpt citation khi score ≥ ngưỡng |
| `SummaryTopK` | 12 | Chunk lấy khi hỏi tóm tắt |
| `SummaryMaxChunks` | 6 | Chunk tối đa đưa vào prompt tóm tắt |
| `SummarySampleEvenly` | true | Lấy mẫu đầu–giữa–cuối tài liệu |
| `SummaryMaxExcerptLength` | 400 | Độ dài đoạn trong prompt tóm tắt |
| `QuizDefaultQuestionCount` | 10 | Số câu quiz mặc định |
| `QuizMinQuestionCount` | 3 | Tối thiểu |
| `QuizMaxQuestionCount` | 30 | Tối đa |

**Chú ý quan trọng:** Đang dùng **mock embedding** (vector từ SHA256, không semantic thật). Ngưỡng `MinSimilarityScore` nên **thấp (0.1–0.15)**. Khi tích hợp embedding thật (OpenAI/Azure), cần **tune lại** các ngưỡng.

### 4.4 AI — `Chat:Ai`

Map vào `AiModelOptions` (`BusinessLogic/Options/AiModelOptions.cs`).

| Thuộc tính | Mặc định | Ý nghĩa |
|------------|----------|---------|
| `Provider` | `Ollama` | `Ollama` hoặc `OpenAI` |
| `Enabled` | true | Tắt → chỉ fallback local |
| `ApiKey` | `ollama` | OpenAI: key thật; Ollama: placeholder |
| `Model` | `llama3.2` | Tên model |
| `BaseUrl` | `http://localhost:11434/v1/` | OpenAI-compatible base URL |
| `Temperature` | 0.3 | Độ ngẫu nhiên |
| `MaxTokens` | 1024 | Token tối đa câu trả lời chat |
| `SummaryMaxTokens` | 512 | Token tối đa cho tóm tắt (thấp = nhanh hơn) |

**Logic cấu hình AI:**

- `IsRemoteAiConfigured()`: Ollama luôn coi là configured; OpenAI cần `ApiKey` hợp lệ (không bắt đầu `YOUR_`).
- `RequiresBearerToken()`: OpenAI có key; Ollama không bắt buộc Bearer.

### 4.5 Logging

```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning"
  }
}
```

Development: thêm `"Microsoft.EntityFrameworkCore": "Information"`.

---

## 5. Khởi động ứng dụng (`Program.cs`)

Thứ tự khi app start:

1. Đọc `DefaultConnection`, đăng ký MVC + Cookie Auth + policy `TeacherOnly`.
2. `AddDbContext<AppDbContext>` — migrations assembly = `Model`.
3. `AddRepositories()` — UnitOfWork + repositories.
4. `Configure` `DocumentStorageOptions`, `RagChatOptions`, `AiModelOptions`.
5. Đăng ký scoped services + `HttpClient` (RAG 90s, AI health 5s, Quiz 120s).
6. **Scope 1:** `TryStartLocalDbAsync` → `MigrateAsync()` → log AI health (không chặn app).
7. **Scope 2:** `EnsureSeedUsersAsync()`.
8. Pipeline: HTTPS, static files, routing, auth, default route `Documents/Index`.

**LocalDB helper:** Gọi `sqllocaldb start MSSQLLocalDB` — lỗi thì chỉ log debug, không crash.

---

## 6. Phân quyền (RBAC)

Chi tiết gốc: `docs/Business-Roles.md`. Tóm tắt:

### 6.1 Ba vai trò

| Role | Mô tả |
|------|--------|
| **Admin** | Quản lý môn, user, gán teacher; dashboard admin; audit |
| **Teacher** | Upload, reindex, generate quiz; dashboard teacher; chỉ môn được gán (lọc theo `TeacherUserId`) |
| **Student** | Xem/tải tài liệu, chat, đăng ký môn; **không** upload/reindex |

### 6.2 Đăng nhập / đăng ký

| Chức năng | URL | Ghi chú |
|-----------|-----|---------|
| Login | `GET/POST /Account/Login` | Cookie 8h sliding; claims: `NameIdentifier`, `Name`, `Role` |
| Register | `GET/POST /Account/Register` | Chỉ tạo **Student** |
| Logout | `POST /Account/Logout` | Ghi audit |

Login field dùng **email** (`LoginViewModel.Email` → `ValidateLoginAsync`).

### 6.3 Policy & attribute

```csharp
options.AddPolicy("TeacherOnly", policy => policy.RequireRole("Teacher"));
```

- `[Authorize]` — đã đăng nhập.
- `[Authorize(Roles = "Admin")]` — chỉ Admin.
- `[Authorize(Policy = "TeacherOnly")]` — chỉ Teacher.

### 6.4 UI theo role

- `Web/Views/Shared/_Layout.cshtml` — menu Upload (Teacher), Admin (Admin).
- `Web/Views/Home/Index.cshtml`, `Documents/Index.cshtml` — ẩn nút Upload với Student.

**Bảo mật:** Ẩn UI **không thay** kiểm tra server. Mật khẩu: PBKDF2 (`PasswordHashHelper`).

### 6.5 Quy tắc gán giáo viên ↔ môn học

`Subject.TeacherUserId` — Admin gán qua `AdminTeachersController`.

| Chiều | Quy tắc |
|-------|---------|
| **Môn → GV** | Mỗi môn **tối đa một** giáo viên; môn đã có GV thì không gán thêm. |
| **GV → Môn** | Một giáo viên có thể phụ trách **nhiều môn** (gán lại cùng email để thêm môn). |

---

## 7. Luồng nghiệp vụ WF1 — Quản lý tài liệu

**Controller:** `Web/Controllers/DocumentsController.cs`  
**Service:** `BusinessLogic/Logic/DocumentService.cs`

### 7.1 Upload & index (Teacher)

```
Upload file
  → Validate extension (.pdf, .docx, .pptx, .ppt)
  → Resolve Subject + Chapter (mỗi chapter tối đa 1 document)
  → Lưu file disk (App_Data/uploads/{guid}.ext)
  → Insert Document (Status = Pending)
  → ProcessDocumentAsync:
        Extract text (DocumentTextExtractor)
        Chunk (ChunkingService: max 800, overlap 100)
        Embedding từng chunk (MockEmbeddingService)
        Lưu DocumentChunk + DocumentEmbedding
        Status = Indexed hoặc Failed
        (Tùy) Generate AI summary → Document.Summary
```

**Trạng thái document** (`DocumentStatus`):

| Giá trị | Ý nghĩa |
|--------|---------|
| Pending | Vừa upload, chưa xử lý |
| Processing | Đang index |
| Indexed | Sẵn sàng RAG |
| Failed | Lỗi (xem `ErrorMessage`) |

### 7.2 Reindex

`POST /Documents/Reindex/{id}` — Teacher chạy lại pipeline (đổi file hoặc sửa lỗi index).

### 7.3 Xem / tải

- **Index:** Danh sách document đã `Indexed`; Teacher chỉ thấy môn mình (`teacherUserId` filter).
- **Details:** Preview text, outline, chunk preview, summary AI, quiz.
- **Download:** Stream file từ disk; ghi audit.

### 7.4 Quiz (Teacher)

- `POST /Documents/GenerateQuiz/{id}` — AI sinh câu hỏi từ nội dung document.
- `GET /Documents/ExportQuiz/{quizId}?format=pdf` — Export (mặc định PDF).

Giới hạn số câu: `QuizMinQuestionCount` … `QuizMaxQuestionCount` trong `Chat:Rag`.

---

## 8. Luồng nghiệp vụ WF2 — Chat RAG

**Controller:** `Web/Controllers/ChatController.cs`  
**Service:** `BusinessLogic/Logic/ChatService.cs`

### 8.1 Sơ đồ Send

```
POST /Chat/Send (JSON: ChatSendRequest)
  │
  ├─ Conversational intent? (ConversationalIntentHelper)
  │     → Trả lời nhanh, không retrieve, không Ollama
  │
  └─ RAG intent
        → Lưu user message
        → Lấy history (MaxHistoryMessages)
        → Retrieve chunks (RetrievalService)
        │     • Câu tóm tắt → RetrieveForSummaryAsync (SummaryTopK)
        │     • Câu thường → RetrieveAsync (TopK)
        → RagAnswerGenerator.GenerateAsync
        │     • Ollama/OpenAI chat/completions
        │     • Lỗi/offline → BuildLocalAnswer (excerpt)
        → Lưu assistant + citations (nếu bật & đủ MinCitationScore)
        → JSON ChatSendResponse
```

### 8.2 Retrieval (`RetrievalService`)

1. Embed câu hỏi (`IEmbeddingService`).
2. Load chunk đã index (lọc `subjectId` nếu phiên chat gắn môn).
3. Cosine similarity (`VectorHelper`).
4. Metadata boost nếu query chứa mã/tên môn/tên file.
5. Lọc theo `MinSimilarityScore` / `FallbackMinScore` (`RagChunkSelector`).
6. Trả `RetrievedChunkDto` top-K.

### 8.3 Sinh câu trả lời (`RagAnswerGenerator`)

1. `RagChunkSelector.TryPrepare` — short-circuit (không có chunk, v.v.).
2. Chọn chunk liên quan (`Select` / `SelectForSummary`).
3. Nếu `Ai.Enabled` + configured → `POST .../chat/completions`.
4. Exception → fallback local excerpt.
5. Câu hỏi summary → prompt riêng, `SummaryMaxTokens`.

### 8.4 Enrollment

`POST /Chat/Enroll` — Student đăng ký môn (`IEnrollmentService`) để lọc/ngữ cảnh chat theo môn.

### 8.5 Phiên chat

- Tạo phiên: `POST /Chat/Create?subjectId=`
- Xem: `GET /Chat/Session/{id}`
- Xóa: `POST /Chat/Delete/{id}` + audit

---

## 9. Bảng endpoint Web

### 9.1 Account

| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET/POST | `/Account/Login` | Anonymous | Đăng nhập |
| GET/POST | `/Account/Register` | Anonymous | Đăng ký Student |
| POST | `/Account/Logout` | Auth | Đăng xuất |

### 9.2 Documents (WF1)

| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/Documents` | Auth | Danh sách tài liệu |
| GET | `/Documents/Details/{id}` | Auth | Chi tiết |
| GET | `/Documents/Download/{id}` | Auth | Tải file |
| GET/POST | `/Documents/Upload` | TeacherOnly | Upload |
| POST | `/Documents/Reindex/{id}` | TeacherOnly | Index lại |
| GET | `/Documents/ChaptersBySubject` | TeacherOnly | JSON chapters (AJAX) |
| POST | `/Documents/GenerateQuiz/{id}` | TeacherOnly | Sinh quiz |
| GET | `/Documents/ExportQuiz/{quizId}` | TeacherOnly | Export quiz |

### 9.3 Chat (WF2)

| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/Chat` | Auth | Danh sách phiên |
| POST | `/Chat/Create` | Auth | Tạo phiên |
| GET | `/Chat/Session/{id}` | Auth | Màn chat |
| POST | `/Chat/Send` | Auth | Gửi câu hỏi (JSON) |
| POST | `/Chat/Enroll` | Auth | Đăng ký môn (JSON) |
| POST | `/Chat/Delete/{id}` | Auth | Xóa phiên |

### 9.4 Admin

| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/AdminSubjects` | Admin | Danh sách môn |
| GET/POST | `/AdminSubjects/Create` | Admin | Tạo môn |
| GET/POST | `/AdminSubjects/AddChapter` | Admin | Thêm chương |
| GET/POST | `/AdminTeachers` | Admin | Gán teacher cho môn |
| GET | `/AdminUsers` | Admin | Redirect → AdminTeachers |
| GET | `/AdminAudit` | Admin | Nhật ký audit |

### 9.5 Dashboard & Home

| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/Dashboard/Teacher` | Teacher | Thống kê teacher |
| GET | `/Dashboard/Admin` | Admin | Thống kê admin |
| GET | `/Home/Index` | — | Trang chủ |
| GET | `/Home/Error` | — | Lỗi (non-dev) |

---

## 10. Cơ sở dữ liệu & Entity

**DbContext:** `Model/Data/AppDbContext.cs`

| DbSet | Mô tả |
|-------|--------|
| `Subjects` | Môn học (Code unique, `TeacherUserId`) |
| `Chapters` | Chương thuộc môn |
| `Documents` | File metadata + status + AI summary |
| `DocumentChunks` | Đoạn text đã chia |
| `DocumentEmbeddings` | Vector JSON per chunk |
| `ChatSessions` | Phiên chat (optional `SubjectId`) |
| `ChatMessages` | User/Assistant messages |
| `MessageCitations` | Trích dẫn chunk/document |
| `SubjectEnrollments` | Student ↔ Subject |
| `AppUsers` | Email, PasswordHash, Role |
| `UserLoginHistories` | Lịch sử đăng nhập |
| `AuditLogs` | Hành động hệ thống |
| `DocumentQuizzes` | Quiz đã sinh |

**Migrations:** `Model/Migrations/` — app tự `MigrateAsync()` khi start.

**Tạo migration thủ công** (từ thư mục solution):

```bash
dotnet ef migrations add TenMigration --project Model --startup-project Web
dotnet ef database update --project Model --startup-project Web
```

---

## 11. Dependency Injection & Services

Đăng ký trong `Web/Program.cs` + `Model/Repository/RepositoryRegistration.cs`.

### 11.1 Repositories (Model)

`IUnitOfWork`, `ISubjectRepository`, `IDocumentRepository`, `IChatRepository`, `IEnrollmentRepository`, `IChunkRepository`, `IUserRepository`, `IAnalyticsRepository`, `IAuditRepository`, `IDocumentQuizRepository`

### 11.2 Business services

| Interface | Implementation | Vai trò |
|-----------|----------------|---------|
| `ISubjectService` | `SubjectService` | Môn/chương |
| `IDocumentService` | `DocumentService` | Upload, index, list, download |
| `IChunkingService` | `ChunkingService` | Chia chunk text |
| `IEmbeddingService` | `MockEmbeddingService` | Vector giả (demo) |
| `IDocumentTextExtractor` | `DocumentTextExtractor` | PDF/DOCX/PPT → text |
| `IRetrievalService` | `RetrievalService` | RAG retrieve |
| `IRagAnswerGenerator` | `RagAnswerGenerator` | Gọi LLM / fallback |
| `IChatService` | `ChatService` | Phiên & send message |
| `IEnrollmentService` | `EnrollmentService` | Đăng ký môn |
| `IDocumentSummaryService` | `DocumentSummaryService` | Tóm tắt sau index |
| `IAuthService` | `AuthService` | Login, register, seed, teacher |
| `IDashboardService` | `DashboardService` | Dashboard metrics |
| `IAuditService` | `AuditService` | Ghi audit log |
| `IAiHealthService` | `AiHealthService` | Kiểm tra Ollama |
| `IQuizService` | `QuizService` | Sinh & export quiz |

### 11.3 Audit actions (`AuditActions`)

`Login`, `Logout`, `Upload`, `Reindex`, `Download`, `CreateTeacher`, `AssignTeacher`, `DeleteChatSession`, `GenerateQuiz`

---

## 12. Tính năng bổ sung

### 12.1 Dashboard

- **Teacher:** `GET /Dashboard/Teacher` — số liệu tài liệu/chat theo teacher.
- **Admin:** `GET /Dashboard/Admin` — tổng quan hệ thống.

### 12.2 Audit

`GET /AdminAudit` — Admin xem log (`IAuditService` / `AuditLog` entity).

### 12.3 Conversational intent

`ConversationalIntentHelper` — nhận diện chào hỏi, hướng dẫn dùng app → trả lời template, **không** tốn retrieve/AI.

### 12.4 Document outline & display

`DocumentOutlineHelper`, `DocumentDisplayHelper` — hiển thị mục lục và preview trên UI Details.

---

## 13. Quy ước code & anti-pattern cần tránh

### 13.1 Nên làm

- Controller mỏng: map ViewModel ↔ DTO, gọi `I*Service`.
- DTO nằm `BusinessLogic/DTOs`, không leak Entity ra Web.
- CancellationToken truyền xuống service/repository.
- Comment workflow ở đầu method phức tạp (đã có mẫu trong `DocumentService`, `ChatService`).

### 13.2 Tránh / ngoại lệ hiện tại

- **`AdminTeachersController`** inject trực tiếp `AppDbContext` — ngoại lệ so với quy tắc “Web không gọi DbContext”. Khi refactor nên chuyển sang `IAuthService` + repository.
- **`AdminUsersController`** chỉ redirect URL cũ → `AdminTeachers`.
- Không commit `bin/`, `obj/`, `.vs/` — đã có trong `.gitignore`.

### 13.3 Thay embedding / AI production

1. Implement `IEmbeddingService` thật (OpenAI/Azure).
2. **Reindex toàn bộ** document sau khi đổi model embedding.
3. Tune `MinSimilarityScore`, `MinCitationScore` trong appsettings.
4. Cấu hình `Chat:Ai` cho OpenAI nếu không dùng Ollama.

---

## 14. Xử lý sự cố thường gặp

| Triệu chứng | Nguyên nhân có thể | Cách xử lý |
|-------------|-------------------|------------|
| App crash khi start, SqlException | LocalDB chưa chạy / sai connection string | Kiểm tra `DefaultConnection`, chạy `sqllocaldb info`, sửa Server |
| Chat trả lời excerpt thô, không “thông minh” | Ollama offline hoặc `Enabled=false` | Bật Ollama, kiểm tra log startup “AI ready” |
| Retrieve không ra chunk | Chưa index / status Failed / ngưỡng similarity quá cao | Reindex; hạ `MinSimilarityScore` |
| Upload báo “chương đã có tài liệu” | Mỗi chapter 1 file | Reindex file cũ hoặc chọn chapter khác |
| Không đăng nhập được | Sai email/password | Dùng `admin@gmail.com` / `123` sau seed |
| Migration lỗi | DB schema lệch | `dotnet ef database update` hoặc xóa DB dev và chạy lại |

---

## 15. Cấu trúc thư mục quan trọng

```
Assigment1/
├── README.md                       ← tài liệu này
├── Assigment1.sln
├── docs/
│   ├── Business-Roles.md           ← RBAC (bản ngắn)
│   └── *.drawio                    ← sơ đồ kiến trúc
├── Web/
│   ├── Program.cs                  ← DI, migrate, seed
│   ├── appsettings.json            ← cấu hình chính
│   ├── Controllers/
│   ├── Views/
│   └── ViewModels/
├── BusinessLogic/
│   ├── DTOs/
│   ├── IBusinessLogic/
│   ├── Logic/                      ← services
│   ├── Options/                    ← RagChatOptions, AiModelOptions
│   └── Helpers/
└── Model/
    ├── Data/AppDbContext.cs
    ├── Entities/
    ├── IRepository/ + Repository/
    └── Migrations/
```

---

## 16. File tham khảo thêm

| File | Nội dung |
|------|----------|
| `docs/Business-Roles.md` | RBAC tóm tắt |
| `docs/Assigment1-Architecture.drawio` | Sơ đồ kiến trúc (draw.io) |
| `Web/appsettings.json` | Cấu hình chạy thực tế |
| `Web/Program.cs` | DI, migrate DB, seed user |

---

*Tài liệu chính của dự án EduAI (Assigment1). Cập nhật README khi đổi endpoint hoặc service.*