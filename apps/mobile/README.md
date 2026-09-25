# SnapLingo Mobile (Flutter)

App Android (Flutter) cho SnapLingo, dựng theo bộ high-fidelity mockup trong
`SnapLingo_Design/` (12 màn hình, bộ "2.7 High-fidelity prototype") và nói
chuyện với API trong [`apps/api`](../api). Luồng học đã được cập nhật theo đề
cương `Proposal_SnapLingo_NoEmotion_Full.docx`: câu chuyện có căn cứ, bộ nhớ cá
nhân, kiểm chứng từng câu và phản hồi một chạm; **chọn cảm xúc là tính năng chờ
phát triển** (code giữ nguyên, ẩn mặc định).

## Chạy app

Yêu cầu: Flutter SDK (stable, Dart ≥ 3.8), Android SDK/emulator, API đang chạy
(xem README gốc).

```bash
cd apps/mobile
flutter pub get

# Emulator Android: mặc định gọi http://10.0.2.2:5164 (= localhost:5164 của máy host,
# profile "http" của SnapLingo.Api). Chạy API bằng profile http:
#   dotnet run --project ../api/src/SnapLingo.Api --launch-profile http
flutter run

# Điện thoại thật cùng mạng Wi-Fi: trỏ vào IP LAN của máy chạy API
flutter run --dart-define=API_BASE_URL=http://192.168.1.10:5164
```

Bản debug có nút **"Dùng tài khoản demo"** ở màn đăng nhập
(`demo@snaplingo.dev` / `Demo123!`, được API seed sẵn).

```bash
flutter analyze
flutter test
flutter build apk --release --dart-define=API_BASE_URL=https://api.example.com
```

## Màn hình ↔ design

| Design | Màn hình | File |
| --- | --- | --- |
| 01 Welcome | Onboarding 4 trang → Đăng ký / Đăng nhập | `features/auth/view/welcome_page.dart` |
| 02 Home | Lời chào, streak, mục tiêu tuần, bài gợi ý, tiếp tục học, ôn tập hôm nay | `features/home/view/home_page.dart` |
| 03 Capture | Camera (flash, đổi camera, zoom 0.5/1x/3), chọn ảnh thư viện | `features/capture/view/capture_page.dart` |
| (mới) Thêm ngữ cảnh | Bước 2/3: thời điểm chụp (sửa được), ghi chú, hoạt động, người trong ảnh (opt-in) — thay cho bước chọn cảm xúc | `features/create_lesson/view/context_page.dart` |
| 04 Emotion | *Tính năng chờ phát triển* — chỉ hiện khi build với `--dart-define=EMOTION_SELECTION=true` | `features/create_lesson/view/emotion_page.dart` |
| 05 AI generating | Tiến độ theo pipeline thật: tải ảnh & ngữ cảnh → ký ức & thói quen → viết câu chuyện → kiểm chứng từng câu → audio | `features/create_lesson/view/generating_page.dart` |
| 06 Lesson overview | Khung cảnh, huy hiệu "x/y phát biểu có căn cứ", "Câu chuyện dựa trên" (thời gian, ghi chú, thói quen, thay đổi, người), từ vựng + lưu tất cả, viết lại / xóa | `features/lesson/view/lesson_overview_page.dart` |
| 07 Vocabulary | IPA, phát âm, nghĩa, câu ví dụ, "mỏ neo trí nhớ", nút 🔖 lưu vào ôn tập | `features/lesson/view/vocabulary_page.dart` |
| 08 Story + transcript | Từng câu có dấu kiểm chứng (✓ có căn cứ / 💡 suy luận) → chạm để xem "Vì sao có câu này?"; báo cáo kiểm chứng; cấu trúc câu; "Người này là ai?" để gắn nhãn & viết lại; chấm điểm 1–5 | `features/lesson/view/story_page.dart` |
| 09 Quick quiz | Trắc nghiệm, phản hồi đúng/sai + giải thích | `features/lesson/view/quiz_page.dart` |
| 10 Review | Flashcard ảnh → nghĩa → Again/Hard/Good/Easy | `features/review/view/review_page.dart` |
| 11 Lesson history | Nhóm theo tháng, mở nhanh Từ vựng/Story/Quiz, xóa | `features/history/view/history_page.dart` |
| 12 Accessibility | Transcript mặc định, highlight từ đang đọc, quiz chỉ text, cỡ chữ | `features/settings/view/accessibility_page.dart` |

Thêm ngoài 12 màn hình: Đăng nhập/Đăng ký, Thống kê ("Tiến độ của bạn" + "Độ
bám sát dữ kiện" và điểm phản hồi trung bình), Cài đặt (hồ sơ, CEFR, tốc độ phát
âm, bản dịch, **gắn nhãn người thân** (opt-in) + trang "Người thân của bạn",
**đồng ý tham gia nghiên cứu**, đăng xuất), màn hoàn thành bài học (kèm chấm điểm).

## Kiến trúc

Theo quy ước của skill Flutter trong repo: feature-first, **Cubit + Equatable**
cho state, service là lớp mỏng quanh một `ApiClient` duy nhất.

```
lib/
  core/        config, theme (AppColors/AppTheme), network (ApiClient), models,
               services (auth/lesson/photo/review/user/speech/progress), settings, widgets
  features/    auth, shell (bottom nav), home, capture, create_lesson, lesson,
               review, history, stats, settings
```

- **Điều hướng**: mỗi tab có `Navigator` riêng (giữ thanh điều hướng dưới khi
  mở bài học, như mockup); camera → cảm xúc → generating, quiz và ôn tập là
  luồng toàn màn hình trên root navigator. `ShellController` nối hai tầng này.
- **JWT** lưu trong `flutter_secure_storage` (Keystore), tự đăng xuất khi API trả 401.
- **Signed URL**: API ký URL ảnh/audio bằng `Storage:PublicBaseUrl`
  (`https://localhost:7182`), không truy cập được từ điện thoại. Chữ ký chỉ phủ
  path + `exp`, nên `ApiClient.resolveMediaUrl` thay origin bằng `API_BASE_URL`.

## Giới hạn hiện tại (do API chưa có)

- **Tiến độ từng bài** (đã xong Từ vựng/Story/Quiz) và streak/mục tiêu tuần được
  tính trên máy từ lịch sử bài học — API chưa có endpoint tiến độ/thống kê.
- **Số từ vựng** ở Thống kê cần tải chi tiết từng bài (có cache) vì
  `GET /lessons` không trả số từ. Thêm `vocabularyCount` + `photoSignedUrl` vào
  `LessonSummaryResponse` sẽ bỏ được các request phụ này.
- **Audio story/hội thoại** lấy từ `POST /lessons/{id}/audio` — hiện API dùng
  TTS giả (tone sin) nên chưa nghe được giọng thật. Phát âm từng từ / câu ví dụ
  và chế độ "đọc to & tô sáng" dùng TTS trên máy (`flutter_tts`).
- Từ vựng chỉ vào danh sách ôn tập khi người học **tự lưu** (🔖) — hành vi này
  cũng là tín hiệu cho Module 5 của đề cương.
- Nút ôn tập hiển thị nhãn (Quên/Khó nhớ/Nhớ/Rất dễ) thay vì khoảng thời gian
  như mockup, vì khoảng ôn thực tế phụ thuộc số lần lặp mà API không trả về.
- **Đăng nhập Google** chưa có trên app (cần OAuth client ID Android từ Google
  Cloud Console); API đã sẵn sàng ở `POST /auth/google`.
- Chỉ có giao diện sáng (mockup chỉ có light mode).
