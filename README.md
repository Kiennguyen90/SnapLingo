# SnapLingo

AI-powered multisensory English learning from everyday photos.

Người dùng chụp một bức ảnh + chọn 1 trong 6 trạng thái cảm xúc (Happy, Calm,
Curious, Excited, Tired, Sad) → AI tạo scene summary, từ vựng, mini-story,
hội thoại, quiz và audio (TTS) → lưu lại cùng ảnh gốc để ôn tập bằng spaced
repetition, dùng chính ảnh làm "memory anchor".

> Photo + Selected Emotion + Learner Profile → AI Lesson → Audio → Practice → Memory Review

## Trạng thái dự án

Backend API đã xong Phase 1–4 theo roadmap trong đặc tả: domain + database +
migrations, **auth thật (đăng ký/đăng nhập + JWT)**, upload ảnh
(validate/strip EXIF/SHA-256), emotion policy, **lesson generation bằng GPT-4o
thật (vision + Structured Outputs)** — có fallback sang fake adapter nếu chưa
config API key — và API tạo/xem/liệt kê/regenerate/xóa lesson + review (spaced
repetition). TTS vẫn còn fake (Phase 5, xem bên dưới). Chuẩn bị build bản
Android prototype để khảo sát người dùng trước khi triển khai production.

### AI thật (GPT-4o) — Phase 4

`ILessonGenerator` có 2 implementation, chọn tự động theo config (không cần
sửa code): `FakeLessonGenerator` (mặc định, deterministic) và
`OpenAiLessonGenerator` (dùng khi có `OpenAi:ApiKey`). Tách thành 2 lớp riêng
biệt để dễ đọc/debug:

- **`OpenAiClient`** (`SnapLingo.Infrastructure/OpenAi/OpenAiClient.cs`) — CHỈ
  biết gọi OpenAI Chat Completions API (model config được, mặc định `gpt-4o`),
  không biết gì về "lesson"/"emotion". Dùng `HttpClient` thuần (không dùng
  OpenAI SDK) nên request/response JSON thấy trực tiếp, dễ debug khi model trả
  sai. Tự retry 429/5xx/timeout (không retry lỗi 4xx khác), fail rõ ràng nếu
  `finish_reason=length` (bị cắt do vượt token).
- **`OpenAiLessonGenerator`** — biết về SnapLingo: build prompt
  (`PromptBuilder`), đọc ảnh từ `IObjectStorage`, gọi `OpenAiClient` với JSON
  Schema (`OpenAiLessonSchema`, Structured Outputs strict mode) khớp đúng
  `GeneratedLesson`, rồi deserialize kết quả.

**Cấu hình API key** (không bao giờ commit key thật vào `appsettings.json`):

```bash
cd apps/api
dotnet user-secrets set "OpenAi:ApiKey" "sk-..." --project src/SnapLingo.Api
```

hoặc set biến môi trường `OpenAi__ApiKey` (dùng cho CI/deployment). Không có
key → tự động fallback `FakeLessonGenerator`, không lỗi, không tốn phí.
`LessonService` validate output của AI thật giống hệt fake adapter (đúng
range vocab theo emotion, quiz correctAnswer nằm trong options...) — nếu
GPT-4o trả sai (có thể xảy ra, LLM không hoàn hảo), lesson bị đánh dấu
`Failed` thay vì lưu sai (AC-09), token vẫn được ghi vào `ai_generation_logs`
vì tiền đã tốn dù output không hợp lệ.

### Auth

Mọi request tới `photos`/`lessons`/`reviews`/`users` đều cần JWT Bearer token
(đăng nhập thật, không còn demo-user bypass qua header):

1. `POST /api/v1/auth/register` — `{ email, password, displayName?, cefrLevel?, nativeLanguage? }` → trả `accessToken`.
2. `POST /api/v1/auth/login` — `{ email, password }` → trả `accessToken` (hết hạn sau `Jwt:AccessTokenMinutes`, mặc định 60 phút).
3. Gửi kèm mọi request khác: header `Authorization: Bearer <accessToken>`.

Tài khoản demo được seed sẵn để test ngay không cần đăng ký:
**`demo@snaplingo.dev` / `Demo123!`**.

Trong Swagger UI (`/swagger`), bấm nút **Authorize** và dán access token vào
để test các endpoint cần đăng nhập trực tiếp trên UI.

### Lesson song ngữ (Anh–Việt)

Mọi phần nội dung học (scene, story, dialogue, quiz, ví dụ từ vựng) đều có
2 phiên bản `*En`/`*Vi`, sinh cùng lúc trong 1 lần gọi AI adapter (không tách
bước dịch riêng) để tiết kiệm chi phí và tránh lệch nghĩa. Audio chỉ đọc bản
tiếng Anh (mục đích luyện phát âm). Riêng đáp án/lựa chọn quiz giữ nguyên
tiếng Anh vì đang kiểm tra đúng từ vựng tiếng Anh.

### Khi nào gọi AI, khi nào chỉ load lại

`POST /lessons` sẽ **tái dùng lesson đã có** thay vì gọi AI lại nếu tồn tại
lesson `Completed` với đúng bộ `(user, photo, emotion, cefrLevel,
nativeLanguage, promptVersion)` — tránh tốn phí khi mở lại cùng ảnh/cảm xúc
hoặc app retry do mất mạng. Đổi emotion/CEFR luôn tạo bộ khoá khác → luôn gọi
AI. `POST /lessons/{id}/regenerate` luôn gọi AI bất kể cache (đây là hành
động chủ động của user, tăng `version`). Đổi `PromptBuilder.PromptVersion`
(vd khi schema output đổi) cũng vô hiệu hoá cache cũ một cách có chủ đích.

### API hiện có (`/api/v1`)

| Method | Route | Auth | Ghi chú |
| --- | --- | --- | --- |
| POST | `/auth/register` | Không | Tạo tài khoản, trả JWT ngay |
| POST | `/auth/login` | Không | Đăng nhập, trả JWT |
| GET | `/users/me` | Có | Thông tin profile của user đang đăng nhập |
| PATCH | `/users/me` | Có | Cập nhật displayName/CEFR/nativeLanguage |
| POST | `/photos` | Có | multipart/form-data; validate MIME/magic-byte/size, strip EXIF/GPS, trả `photoId` + signed URL |
| POST | `/lessons` | Có | Sinh lesson từ `photoId` + `emotion` (fake AI adapter theo `EmotionPolicy`) |
| GET | `/lessons/{id}` | Có | Lesson đầy đủ (scene/vocabulary/story/dialogue/quiz/audio) |
| GET | `/lessons?page=&pageSize=` | Có | Lịch sử lesson của user đang đăng nhập |
| POST | `/lessons/{id}/regenerate` | Có | Cùng ảnh, emotion khác, tăng `version` |
| POST | `/lessons/{id}/audio` | Có | Sinh audio story/dialogue on-demand (fake TTS), cache theo hash |
| GET | `/reviews/today` | Có | Từ vựng đến hạn ôn, kèm ảnh gốc làm memory anchor |
| POST | `/reviews/{vocabularyId}/answer` | Có | Gửi `Forgot/Hard/Good/Easy`, cập nhật lịch ôn 1-3-7-14 ngày |
| DELETE | `/lessons/{id}` | Có | Xóa lesson (thu hồi quyền truy cập audio của lesson đó) |
| GET | `/media/{**objectKey}` | Không (signed URL) | Phục vụ ảnh/audio qua signed URL có thời hạn, không public bucket — bảo vệ bằng chữ ký HMAC + `exp` riêng, không dùng JWT |

Toàn bộ dữ liệu (photo/lesson/vocabulary progress) được gắn với đúng `userId`
lấy từ JWT (`sub` claim) — mỗi user chỉ thấy dữ liệu của chính mình.

## Chạy API local

Yêu cầu: .NET 9 SDK, Docker Desktop. (Dự án hạ từ .NET 10 xuống .NET 9 để
tương thích với Visual Studio phiên bản hiện tại — cần VS 17.16+ mới target
được .NET 10.)

Mở IDE (Visual Studio, Rider, VS Code) bằng file solution
[`apps/api/SnapLingo.slnx`](./apps/api/SnapLingo.slnx) — gom cả 7 project
(5 src + 2 tests). Mở đúng thư mục `SnapLingo` này làm workspace gốc
(`File > Open Folder` → chọn thư mục chứa `.git`), **không** mở thư mục cha
chứa các file môn học khác, kẻo run config bị lệch cấp thư mục và không
chạy được. Trong VS Code, nhấn `F5` (hoặc Run and Debug →
"SnapLingo.Api (Development)") sẽ build + chạy API theo cấu hình sẵn trong
[`.vscode/launch.json`](./.vscode/launch.json).

Hoặc chạy thẳng bằng dòng lệnh, không cần mở IDE:

```bash
# 1. Copy .env mẫu (docker compose đọc các biến này)
cp .env.example .env

# 2. Chạy PostgreSQL
docker compose -f infra/docker-compose.yml up -d

# 3. Áp dụng migration (lần đầu hoặc sau khi đổi model)
cd apps/api
dotnet tool restore
dotnet ef database update --project src/SnapLingo.Infrastructure --startup-project src/SnapLingo.Api

# 4. Chạy API
dotnet run --project src/SnapLingo.Api
```

- Swagger UI: `https://localhost:<port>/swagger`
- Health check: `GET /health` (kiểm tra kết nối PostgreSQL)
- Chạy test: `dotnet test` (trong `apps/api`)

Cấu trúc solution theo đúng đặc tả: `SnapLingo.Domain` (entities/enums/value
objects thuần, không phụ thuộc EF) → `SnapLingo.Application` (use-case
services: `AuthService`, `UserService`, `PhotoService`, `LessonService`,
`AudioService`, `ReviewService`, `EmotionPolicy`, `PromptBuilder`, và các
interface `ITokenService`/`ILessonGenerator`/`ITextToSpeechService`/
`IObjectStorage` mà Infrastructure implement) → `SnapLingo.Infrastructure`
(EF Core DbContext, configurations, migrations, Npgsql, local object
storage, `JwtTokenService`, `FakeLessonGenerator`/`OpenAiLessonGenerator`/
`OpenAiClient`, `FakeTextToSpeechService`) → `SnapLingo.Contracts`
(Request/Response DTO cho API) → `SnapLingo.Api` (minimal API endpoints,
JWT bearer auth, exception handling, Swagger, health check).

`ILessonGenerator` trỏ vào `OpenAiLessonGenerator` (GPT-4o thật) nếu có
`OpenAi:ApiKey`, ngược lại `FakeLessonGenerator` — xem mục "AI thật (GPT-4o)"
ở trên. `ITextToSpeechService` vẫn trỏ `FakeTextToSpeechService` (Phase 5
chưa làm).

## Tài liệu trong repo

- [`SnapLingo_Project_Specification_AI_Code_Prompting.docx`](./SnapLingo_Project_Specification_AI_Code_Prompting.docx) —
  đặc tả sản phẩm + kỹ thuật + prompting guide đầy đủ (mô tả bài toán, emotion
  system, kiến trúc, API contract, database design, AI/LLM prompts, master
  prompt cho coding agent, test matrix).
- [`snaplingo-project-review.html`](./snaplingo-project-review.html) — bản
  đánh giá đặc tả trên: điểm mạnh, khoảng trống và các quyết định đã chốt
  (validate bằng prototype, kiến trúc lai CNN on-device để giảm chi phí AI,
  chính sách kiểm duyệt nội dung, roadmap).

## Định hướng kiến trúc chính

- **Mobile**: Flutter (Android trước, iOS sau khi validate).
- **Backend**: ASP.NET Core Web API + PostgreSQL (EF Core/Npgsql) + object
  storage cho ảnh/audio.
- **AI**: OpenAI multimodal model đứng sau abstraction `ILessonGenerator`
  (model lấy từ config, không hard-code).
- **Bảo mật**: đăng nhập bằng JWT Bearer token (mật khẩu hash bằng
  `PasswordHasher<User>`, không lưu plaintext); API key OpenAI chỉ ở backend;
  strip EXIF/GPS mặc định; signed URL có thời hạn cho ảnh/audio; rate limit
  theo user/device (chưa implement).

## Roadmap: 2 giai đoạn

**Giai đoạn 1 — MVP Vision-only (không CNN)**
1. Build Android APK cho luồng Camera → Emotion Picker → Lesson → Review.
2. Gửi ảnh thẳng cho GPT-4o (vision) sinh scene/vocabulary/story/dialogue/quiz.
3. Tách sẵn `ISceneTagger`/`ILessonGenerator` dù chỉ có 1 implementation
   (CloudVision), để giai đoạn 2 cắm CNN vào không phải sửa business logic.
4. Phát APK kèm form khảo sát ngắn cho người dùng thử nghiệm nội bộ.

**Giai đoạn 2 — CNN on-device + Report đánh giá**
1. Thêm CNN nhẹ chạy on-device (Google ML Kit / TFLite) gắn tag ảnh, gửi
   text tag cho LLM thay vì gửi ảnh — giảm token/cost.
2. Đo song song trên cùng bộ ảnh test: token/cost, latency, độ chính xác
   vocabulary giữa pipeline Vision-only (giai đoạn 1) và CNN-hybrid.
3. Xuất report đánh giá so sánh 2 pipeline, dùng số liệu đó để quyết định
   tier mặc định và ngưỡng fallback về Cloud Vision.

Sau khi có phản hồi thực tế từ 2 giai đoạn trên mới làm phân tích cạnh tranh
và mô hình kiếm tiền chính thức. Chi tiết đầy đủ xem trong hai tài liệu ở
trên.
