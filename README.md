# SnapLingo

AI-powered multisensory English learning from everyday photos.

Người dùng chụp một bức ảnh + chọn 1 trong 6 trạng thái cảm xúc (Happy, Calm,
Curious, Excited, Tired, Sad) → AI tạo scene summary, từ vựng, mini-story,
hội thoại, quiz và audio (TTS) → lưu lại cùng ảnh gốc để ôn tập bằng spaced
repetition, dùng chính ảnh làm "memory anchor".

> Photo + Selected Emotion + Learner Profile → AI Lesson → Audio → Practice → Memory Review

## Trạng thái dự án

Giai đoạn ý tưởng / đặc tả kỹ thuật, chuẩn bị build bản Android prototype đầu
tiên để khảo sát người dùng trước khi triển khai production.

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
- **Kiến trúc lai giảm chi phí**: CNN nhẹ chạy on-device (Google ML Kit /
  TFLite) gắn tag ảnh ở free tier, chỉ gửi text tag cho LLM thay vì gửi ảnh
  cho vision model; Cloud Vision đầy đủ dùng làm fallback/tier cao cấp — cả
  hai đứng sau cùng `ILessonGenerator`.
- **Bảo mật**: API key OpenAI chỉ ở backend, strip EXIF/GPS mặc định, signed
  URL có thời hạn cho ảnh/audio, rate limit theo user/device.

## Roadmap ngắn hạn

1. Build Android APK cho luồng Camera → Emotion Picker → Lesson → Review.
2. Tách `ISceneTagger` (on-device) khỏi `ILessonGenerator` (cloud) ngay từ đầu.
3. Phát APK kèm form khảo sát ngắn cho người dùng thử nghiệm nội bộ.
4. Dựa trên phản hồi thực tế để làm phân tích cạnh tranh và mô hình kiếm tiền.

Chi tiết đầy đủ xem trong hai tài liệu ở trên.
