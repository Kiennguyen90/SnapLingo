# SnapLingo

AI-powered multisensory English learning from everyday photos.

Người dùng chụp một bức ảnh + chọn 1 trong 6 trạng thái cảm xúc (Happy, Calm,
Curious, Excited, Tired, Sad) → AI tạo scene summary, từ vựng, mini-story,
hội thoại, quiz và audio (TTS) → lưu lại cùng ảnh gốc để ôn tập bằng spaced
repetition, dùng chính ảnh làm "memory anchor".

> Photo + Selected Emotion + Learner Profile → AI Lesson → Audio → Practice → Memory Review

## Trạng thái dự án

Backend API đã có khung sườn cơ bản (Phase 1: domain + database + migrations,
chưa nối OpenAI). Chuẩn bị build bản Android prototype để khảo sát người dùng
trước khi triển khai production.

## Chạy API local

Yêu cầu: .NET 10 SDK, Docker Desktop.

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

Cấu trúc solution theo đúng đặc tả: `SnapLingo.Domain` (entities/enums thuần,
không phụ thuộc EF) → `SnapLingo.Infrastructure` (EF Core DbContext,
configurations, migrations, Npgsql) → `SnapLingo.Api` (Program.cs, health
check, Swagger). Chưa có `SnapLingo.Application`/`SnapLingo.Contracts` — sẽ
thêm khi có logic/DTO thật ở phase sinh lesson (tránh project rỗng).

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
- **Bảo mật**: API key OpenAI chỉ ở backend, strip EXIF/GPS mặc định, signed
  URL có thời hạn cho ảnh/audio, rate limit theo user/device.

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
