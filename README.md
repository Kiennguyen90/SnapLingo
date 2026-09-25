# SnapLingo

AI-powered multisensory English learning from everyday photos.

Người học chụp ảnh sinh hoạt hằng ngày (kèm thời gian chụp, ghi chú/hoạt động và
— nếu tự bật — nhãn người thân) → hệ thống sinh một **câu chuyện song ngữ
Việt–Anh có căn cứ**, gắn với thói quen và ký ức của chính người học, kèm từ
vựng, cấu trúc câu, hội thoại, quiz và audio → **mỗi câu được kiểm chứng** với
ảnh, bộ nhớ, metadata và nhãn người trước khi hiển thị → ôn tập bằng spaced
repetition với chính bức ảnh làm "memory anchor".

> Photo + Metadata + Personal Memory → Evidence → Story → Multi-source Verification → Practice → Review

Thiết kế hiện tại theo đề cương luận văn
`SnapLingo_Design/Proposal_SnapLingo_NoEmotion_Full.docx` ("Sinh câu chuyện học
tiếng Anh cá nhân hóa từ ảnh đời thường, có căn cứ dữ kiện và bộ nhớ cá nhân dài
hạn"). **Tính năng chọn cảm xúc** của thiết kế cũ được giữ lại trong code như
một tính năng chờ phát triển sau — tắt mặc định (`Features:EmotionSelection`,
và `--dart-define=EMOTION_SELECTION=true` ở app).

## Trạng thái dự án

Backend API đã xong Phase 1–4 theo roadmap trong đặc tả: domain + database +
migrations, **auth thật (đăng ký/đăng nhập + JWT)**, upload ảnh
(validate/strip EXIF/SHA-256), **lesson generation bằng GPT-4o thật (vision +
Structured Outputs)** — có fallback sang fake adapter nếu chưa config API key —
và API tạo/xem/liệt kê/regenerate/xóa lesson + review (spaced repetition).
Đã chuyển sang **pipeline câu chuyện có căn cứ + bộ nhớ cá nhân** theo đề cương
(mục dưới); cần chạy migration `GroundedMemoryStory`. TTS vẫn còn fake (Phase 5).
App Android (Flutter) ở `apps/mobile`.

### Câu chuyện có căn cứ & bộ nhớ cá nhân (theo đề cương)

`LessonService` chạy đủ 5 module của đề cương (mục 5.2–5.4) cho mỗi bài học:

| Module | Code | Việc làm |
| --- | --- | --- |
| 1 · Visual Understanding & Event Extraction | `IEventExtractor` (`OpenAiEventExtractor` / `FakeEventExtractor`) | Ảnh → bản ghi sự kiện: cảnh, `placeKey`, đồ vật, hoạt động, số người. Lưu 1 lần/ảnh vào `photo_events` |
| 2 · Personal Multimodal Memory | `MemoryService` + `ITextEmbedder` | Embedding + giờ địa phương cho mỗi sự kiện; truy xuất top‑k sự kiện **trong quá khứ** theo điểm kết hợp ngữ nghĩa + độ gần thời gian + mức lặp lại cùng địa điểm |
| 3 · Temporal Modeling | `TemporalAnalyzer` (thuần, không I/O) | Thói quen (≥3 lần cùng nơi, cùng khung giờ trong 28 ngày) và thay đổi ngữ cảnh (cuối tuần vs ngày thường, lệch giờ, thiếu/mới đồ vật) |
| 4a · Story Generator | `ILessonGenerator` + `PromptBuilder` (`lesson-v2.0.0`) | Nhận danh sách căn cứ có mã (`I` ảnh, `T/N/A` metadata, `M` ký ức, `H` thói quen, `C` thay đổi, `P/U` người) → story song ngữ **từng câu kèm mã căn cứ**, từ vựng, cấu trúc câu, hội thoại, quiz |
| 4b · Multi-source Grounding Verifier | `GroundingVerifier` = `IClaimJudge` (model tách/đánh giá phát biểu, có nhìn ảnh) + `ClaimRules` (luật: thời gian, nhãn người, thói quen) | Gắn nhãn *có căn cứ / suy luận / không có căn cứ*; câu không có căn cứ được gửi lại để sinh lại (tối đa `MaxRegenerations`), còn lại thì **loại**; suy luận chưa rào đón thì thêm "It seems that…" |
| 5 · Personalization & Satisfaction | `POST /lessons/{id}/feedback`, lưu từ ôn tập | Phản hồi một chạm 1–5 (hài lòng, hữu ích); người học **tự chọn** từ để ôn (không còn tự thêm mọi từ) |

Mỗi bài học lưu kèm `evidence_json` (danh sách căn cứ) và `grounding_json`
(báo cáo kiểm chứng: từng câu, từng phát biểu, nguồn, hành động, số liệu vòng
đầu/vòng cuối) để phân tích RQ1–RQ4. Điều kiện thí nghiệm được cấu hình trong
`StoryPipeline` và ghi lên từng lesson (`MemoryMode`, `VerifierMode`):
`MemoryMode = None|Personal` (E2), `VerifierMode = None|ImageOnly|MultiSource`
(E3), `Assignment = AlternateWithBaseline` để luân phiên phiên bản gốc/đề xuất
theo từng bài cho cùng người dùng (E5).

**Nhãn người (opt-in, mục 3.4):** dùng phương án dự phòng của đề cương — người
học tự gắn nhãn người trong ảnh (`/people`, `PUT /photos/{id}/people`); không
lưu ảnh hay vector khuôn mặt, không bao giờ đoán danh tính người lạ. Story chỉ
được nhắc tên/quan hệ của người đã gắn nhãn; người chưa gắn nhãn chỉ được mô
tả chung chung có rào đón, và app hỏi "Người này là ai?" để viết lại. Xóa một
người sẽ xóa mọi nhãn của họ. `PATCH /users/me` có `personLabelingEnabled` và
`researchConsent` (đồng ý tham gia nghiên cứu).

Không có OpenAI key, toàn bộ pipeline vẫn chạy bằng adapter giả xác định
(`FakeEventExtractor`, `FakeLessonGenerator`, `CitationClaimJudge`) và một
embedder thật nhưng miễn phí (`HashingTextEmbedder`).

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
3. `POST /api/v1/auth/google` — `{ idToken }` → trả `accessToken` cùng shape với 2 endpoint trên (xem mục "Đăng nhập Google" dưới).
4. Gửi kèm mọi request khác: header `Authorization: Bearer <accessToken>`.

Tài khoản demo được seed sẵn để test ngay không cần đăng ký:
**`demo@snaplingo.dev` / `Demo123!`**.

Trong Swagger UI (`/swagger`), bấm nút **Authorize** và dán access token vào
để test các endpoint cần đăng nhập trực tiếp trên UI.

### Đăng nhập Google

Theo đúng flow ID-token chuẩn cho app mobile (Flutter/Android là target chính
của SnapLingo): client dùng Google Sign-In SDK để lấy **ID token**, gửi
nguyên token đó cho backend — backend không bao giờ thấy mật khẩu Google của
người dùng.

- **`IGoogleIdTokenValidator`** (`GoogleIdTokenValidator` trong
  Infrastructure) verify chữ ký RS256 của token so với bộ public key Google
  công bố ở `GoogleAuth:CertsUrl` (mặc định
  `https://www.googleapis.com/oauth2/v3/certs`, cache
  `GoogleAuth:CertsCacheMinutes` phút), kiểm tra `issuer`/`audience`/hết hạn
  bằng chính `System.IdentityModel.Tokens.Jwt` đã dùng cho JWT nội bộ — không
  thêm SDK Google riêng (cùng triết lý "HttpClient thuần" với `OpenAiClient`).
  Audience phải khớp 1 trong `GoogleAuth:AllowedClientIds` (mỗi platform
  Android/iOS/Web có client ID riêng từ Google Cloud Console).
- **`AuthService.GoogleLoginAsync`** — tìm user theo `GoogleId`; nếu chưa có
  nhưng đã tồn tại user với cùng email (đăng ký bằng mật khẩu trước đó) thì
  **link** `GoogleId` vào user đó (không tạo trùng); nếu chưa tồn tại luôn
  thì tạo user mới không có mật khẩu (`PasswordHash = ""`). Bắt buộc
  `email_verified = true` từ Google, và nếu email đã bị link với 1 Google
  account khác thì từ chối (chống chiếm đoạt tài khoản qua email trùng).
- User chỉ đăng nhập bằng Google (chưa từng đặt mật khẩu) gọi
  `POST /auth/login` bằng mật khẩu sẽ nhận lỗi 401 rõ ràng thay vì đăng nhập
  được bằng mật khẩu rỗng.
- **Cấu hình**: đặt `GoogleAuth:AllowedClientIds` (mảng client ID lấy từ
  Google Cloud Console) qua `appsettings.json`/`dotnet user-secrets`/env
  `GoogleAuth__AllowedClientIds__0`. Để trống → `POST /auth/google` trả lỗi
  401 "chưa cấu hình", không crash server.

### Lesson song ngữ (Anh–Việt)

Mọi phần nội dung học (scene, story, dialogue, quiz, ví dụ từ vựng) đều có
2 phiên bản `*En`/`*Vi`, sinh cùng lúc trong 1 lần gọi AI adapter (không tách
bước dịch riêng) để tiết kiệm chi phí và tránh lệch nghĩa. Dialogue audio chỉ
đọc bản tiếng Anh (mục đích luyện phát âm); story audio đọc **cả 2 bản** (xem
mục dưới). Riêng đáp án/lựa chọn quiz giữ nguyên tiếng Anh vì đang kiểm tra
đúng từ vựng tiếng Anh.

### Nhạc nền cho ảnh & audio story song ngữ

Hai adapter mới theo cùng khuôn `ILessonGenerator`/`ITextToSpeechService`
(interface ở Application, fake ở Infrastructure, đổi implementation không
cần sửa code gọi):

- **`IBackgroundMusicService`** (`PhotoMusicService`) — gắn 1 track nhạc nền
  cho mỗi ảnh, chọn theo `emotion` (`POST /photos/{id}/music?emotion=Happy`,
  mặc định `Calm` để nhạc nền không lấn lướt). Cache theo hash
  `(model, mood)`: cùng ảnh gọi lại cùng mood → không tổng hợp lại; hai ảnh
  khác nhau cùng mood → dùng lại chung file trong object storage (giống cơ
  chế cache của `AudioService`). Mỗi ảnh chỉ giữ 1 track "hiện tại" — đổi
  mood sẽ ghi đè. `FakeBackgroundMusicService` tạo 1 tone sóng sin xác định
  theo mood (không cần thư viện nhạc ngoài) làm placeholder cho một provider
  nhạc AI thật sau này.
- **Story audio song ngữ** — `POST /lessons/{id}/audio` giờ trả thêm
  `storyViUrl`: cùng nội dung story, đọc bằng giọng tiếng Việt riêng
  (`Audio:VietnameseVoice` trong config), lưu ở `audio_assets` với
  `kind = "story_vi"` (tách khỏi `"story"` tiếng Anh nên không đụng cache
  cũ). Dialogue vẫn chỉ tiếng Anh như trước.

### Khi nào gọi AI, khi nào chỉ load lại

`POST /lessons` sẽ **tái dùng lesson đã có** thay vì gọi AI lại nếu tồn tại
lesson `Completed` với đúng bộ `(user, photo, emotion, cefrLevel,
nativeLanguage, promptVersion)` — tránh tốn phí khi app retry do mất mạng
(emotion luôn `null` khi tính năng cảm xúc tắt). Sự kiện của ảnh (Module 1)
cũng chỉ trích xuất một lần và dùng lại cho mọi phiên bản bài học. `POST /lessons/{id}/regenerate` luôn gọi AI bất kể cache (đây là hành
động chủ động của user, tăng `version`). Đổi `PromptBuilder.PromptVersion`
(vd khi schema output đổi) cũng vô hiệu hoá cache cũ một cách có chủ đích.

### API hiện có (`/api/v1`)

| Method | Route | Auth | Ghi chú |
| --- | --- | --- | --- |
| POST | `/auth/register` | Không | Tạo tài khoản, trả JWT ngay |
| POST | `/auth/login` | Không | Đăng nhập, trả JWT |
| GET | `/users/me` | Có | Thông tin profile của user đang đăng nhập |
| PATCH | `/users/me` | Có | Cập nhật displayName/CEFR/nativeLanguage, `personLabelingEnabled`, `researchConsent` |
| POST | `/photos` | Có | multipart/form-data: `file` + (tùy chọn) `capturedAt` (ISO 8601 có offset), `note`, `activityTag`; validate MIME/magic-byte/size, strip EXIF/GPS |
| PATCH | `/photos/{id}` | Có | Sửa ghi chú / hoạt động của ảnh |
| GET/PUT | `/photos/{id}/people` | Có | Người đã gắn nhãn trong ảnh + số người chưa gắn nhãn / thay toàn bộ nhãn |
| GET | `/people/relations` | Có | Danh sách quan hệ cho phép (Mẹ, Em trai, Bà ngoại…) |
| GET/POST/PATCH/DELETE | `/people[/{id}]` | Có | Người thân đã gắn nhãn (opt-in); xóa = gỡ mọi nhãn |
| GET | `/photos/{id}` | Có | Lấy lại ảnh + nhạc nền hiện tại (`music: null` nếu chưa gắn) |
| POST | `/photos/{id}/music` | Có | Gắn nhạc nền (fake) theo `emotion` (query, mặc định `Calm`) cho ảnh, cache theo mood+model |
| POST | `/lessons` | Có | Chạy pipeline có căn cứ từ `photoId` (`emotion` tùy chọn, bị bỏ qua khi tính năng tắt) |
| GET | `/lessons/{id}` | Có | Lesson đầy đủ: story từng câu kèm nhãn + nguồn, cấu trúc câu, báo cáo kiểm chứng, ngữ cảnh, phản hồi, từ đã lưu |
| GET | `/lessons?page=&pageSize=` | Có | Lịch sử lesson của user đang đăng nhập |
| POST | `/lessons/{id}/regenerate` | Có | Viết lại story từ cùng ảnh (vd sau khi gắn nhãn người), tăng `version` |
| POST | `/lessons/{id}/feedback` | Có | Phản hồi một chạm `satisfaction`/`usefulness` (1–5) |
| POST/DELETE | `/lessons/{id}/vocabulary/{vocabularyId}/save` | Có | Lưu / bỏ lưu một từ vào ôn tập |
| POST | `/lessons/{id}/vocabulary/save-all` | Có | Lưu mọi từ của bài vào ôn tập |
| POST | `/lessons/{id}/audio` | Có | Sinh audio story (Anh + Việt) và dialogue (Anh) on-demand (fake TTS), cache theo hash |
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
`AudioService`, `PhotoMusicService`, `ReviewService`, `EmotionPolicy`,
`PromptBuilder`, và các interface `ITokenService`/`ILessonGenerator`/
`ITextToSpeechService`/`IBackgroundMusicService`/`IGoogleIdTokenValidator`/
`IObjectStorage` mà Infrastructure implement) → `SnapLingo.Infrastructure`
(EF Core DbContext, configurations, migrations, Npgsql, local object
storage, `JwtTokenService`, `GoogleIdTokenValidator`, `FakeLessonGenerator`/
`OpenAiLessonGenerator`/`OpenAiClient`, `FakeTextToSpeechService`,
`FakeBackgroundMusicService`) → `SnapLingo.Contracts`
(Request/Response DTO cho API) → `SnapLingo.Api` (minimal API endpoints,
JWT bearer auth, exception handling, Swagger, health check).

`ILessonGenerator` trỏ vào `OpenAiLessonGenerator` (GPT-4o thật) nếu có
`OpenAi:ApiKey`, ngược lại `FakeLessonGenerator` — xem mục "AI thật (GPT-4o)"
ở trên. `ITextToSpeechService` vẫn trỏ `FakeTextToSpeechService` (Phase 5
chưa làm).

## App mobile (Flutter)

App Android theo bộ mockup high-fidelity (`SnapLingo_Design/`) nằm ở
[`apps/mobile`](./apps/mobile) — đủ luồng Chụp ảnh → Chọn cảm xúc → AI tạo bài
→ Từ vựng → Câu chuyện → Quiz → Ôn tập, gọi thẳng các API ở trên. Chạy API bằng
profile `http` rồi:

```bash
cd apps/mobile
flutter pub get
flutter run   # emulator Android gọi http://10.0.2.2:5164
```

Chi tiết (cấu hình `API_BASE_URL`, bảng màn hình ↔ design, giới hạn hiện tại)
xem [`apps/mobile/README.md`](./apps/mobile/README.md).

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
