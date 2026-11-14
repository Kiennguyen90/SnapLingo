# BillHub — Tự động thêm Custom Domain (Reverse Proxy + SSL) trên Synology DSM 7.2

Script `billhub-domain.sh` cho phép mỗi công ty dùng domain riêng (ví dụ `crm.congty.vn`) thay cho domain gốc của BillHub. Một lệnh duy nhất sẽ:

1. Kiểm tra DNS của domain khách đã trỏ về NAS chưa.
2. Tạo rule Reverse Proxy bằng cách clone y hệt rule của domain gốc (cùng backend `localhost:5082`, timeout, header, ACL).
3. Xin chứng chỉ Let's Encrypt bằng công cụ có sẵn của DSM (`syno-letsencrypt`).
4. Gán chứng chỉ cho đúng rule vừa tạo và xác minh HTTPS qua SNI.
5. DSM tự gia hạn chứng chỉ; lệnh `sync` chạy hằng ngày làm lưới an toàn.

App (chạy trong Docker) **không cần quyền root trên NAS**: chỉ cần ghi một file rỗng vào thư mục hàng đợi, script trên host sẽ xử lý.

---

## 1. Kiến trúc

```
Khách hàng                      NAS (Synology DSM 7.2)
──────────                      ───────────────────────────────────────────────
crm.congty.vn  ──CNAME──▶  billhub.sunmedia.vn ──▶ Router :80/:443 ──▶ DSM nginx
                                                                     │
                                       Reverse Proxy rule (mỗi domain 1 rule)
                                                                     ▼
                                                     Docker app  localhost:5082

App ──ghi file──▶ domain-queue/crm.congty.vn.add
Task Scheduler (root, 5 phút) ──▶ billhub-domain.sh queue
                                  ├─ tạo Reverse Proxy (synowebapi)
                                  ├─ xin cert (syno-letsencrypt)
                                  ├─ gán cert vào rule
                                  └─ ghi domain-queue/crm.congty.vn.result
```

Công cụ DSM được dùng:

| Công cụ | Vai trò |
|---|---|
| `/usr/syno/bin/synowebapi` | Gọi API nội bộ của DSM (giống giao diện web): `SYNO.Core.AppPortal.ReverseProxy`, `SYNO.Core.Certificate.CRT`, `SYNO.Core.Certificate.Service` |
| `/usr/syno/sbin/syno-letsencrypt` | Xin chứng chỉ Let's Encrypt (HTTP-01 qua port 80) |
| `/usr/syno/bin/synow3tool` | Sinh lại cấu hình nginx/cert (dùng khi cần copy cert thủ công) |

> Lưu ý: các API Reverse Proxy và Certificate **không có trong tài liệu chính thức** của Synology. Sau mỗi lần cập nhật DSM nên test lại một domain.

---

## 2. Yêu cầu

- Synology DSM 7.2, có quyền SSH và `sudo -i` lên root.
- Router forward **port 80 và 443** về NAS (port 80 bắt buộc cho xin và gia hạn chứng chỉ).
- Rule Reverse Proxy của domain gốc (`TEMPLATE_FQDN`) đang chạy OK trên port 443.
- Có sẵn trên DSM: `jq`, `curl`, `openssl` (DSM 7 đã có).

---

## 3. Cài đặt

```bash
sudo -i
mkdir -p /volume1/scripts
cp billhub-domain.sh /volume1/scripts/
chmod 700 /volume1/scripts/billhub-domain.sh

# thư mục hàng đợi, mount vào container của app
mkdir -p /volume1/docker/billhub/domain-queue
```

### Cấu hình (đầu file script)

| Biến | Ý nghĩa | Giá trị hiện tại |
|---|---|---|
| `TEMPLATE_FQDN` | Domain gốc, rule của nó được clone cho domain mới | `billhub.sunmedia.vn` |
| `LE_EMAIL` | Email đăng ký Let's Encrypt | email của bạn |
| `QUEUE_DIR` | Thư mục hàng đợi app ghi yêu cầu | `/volume1/docker/billhub/domain-queue` |
| `LOG_FILE` | File log | `/var/log/nas-domain.log` |
| `DESC_PREFIX` | Tiền tố mô tả rule do script tạo (script chỉ xóa rule có tiền tố này) | `billhub:` |
| `RENEW_WARN_DAYS` | Cảnh báo khi cert còn ít hơn số ngày này | `14` |

### Task Scheduler

Control Panel > Task Scheduler > Create > Scheduled Task > User-defined script, **User: root**:

| Task | Lịch chạy | Lệnh |
|---|---|---|
| BillHub domain queue | Hằng ngày, lặp mỗi 5 phút | `/volume1/scripts/billhub-domain.sh queue` |
| BillHub cert sync | Hằng ngày, 1 lần (ví dụ 03:30) | `/volume1/scripts/billhub-domain.sh sync` |

---

## 4. Sử dụng bằng tay (SSH, root)

```bash
# thêm domain
/volume1/scripts/billhub-domain.sh add crm.congty.vn

# xóa domain (chỉ xóa rule do script tạo)
/volume1/scripts/billhub-domain.sh remove crm.congty.vn

# liệt kê domain đang quản lý + ngày hết hạn cert
/volume1/scripts/billhub-domain.sh list

# đồng bộ cert sau gia hạn + cảnh báo cert sắp hết hạn
/volume1/scripts/billhub-domain.sh sync

# xử lý hàng đợi do app ghi
/volume1/scripts/billhub-domain.sh queue
```

> **Không dùng `--staging`.** Môi trường staging của Let's Encrypt dùng cơ chế hoàn tất đơn bất đồng bộ mà `syno-letsencrypt` không hỗ trợ, nên luôn fail ở bước cuối với lỗi `Server is not reachable`.

Lệnh `add` an toàn khi chạy lại: nếu rule đã có thì bỏ qua bước tạo rule; nếu HTTPS đã hợp lệ thì bỏ qua toàn bộ.

### Exit code

| Code | Ý nghĩa |
|---|---|
| 0 | Thành công |
| 1 | Sai tham số / môi trường (không phải root, thiếu lệnh, domain không hợp lệ) |
| 2 | DNS chưa trỏ đúng về NAS (hoặc có AAAA trỏ sai) |
| 3 | Lỗi tạo/xóa Reverse Proxy |
| 4 | Lỗi xin chứng chỉ (`syno-letsencrypt`) |
| 5 | Lỗi gán chứng chỉ vào rule |

---

## 5. Tích hợp với app (hàng đợi)

App chỉ cần tạo **file rỗng**, tên file là domain viết thường:

```
domain-queue/crm.congty.vn.add      # thêm domain
domain-queue/crm.congty.vn.remove   # xóa domain
```

Script (chạy qua Task Scheduler) xóa file yêu cầu, xử lý lần lượt từng domain, rồi ghi kết quả:

```json
// domain-queue/crm.congty.vn.result
{
  "domain": "crm.congty.vn",
  "action": "add",
  "ok": true,
  "code": 0,
  "message": "2026-10-30 23:20:01 [1234] OK: https://crm.congty.vn",
  "at": "2026-10-30T23:20:01+0700"
}
```

| `code` | App nên làm |
|---|---|
| 0 | Đánh dấu domain active |
| 2 | Báo khách kiểm tra DNS, cho phép thử lại sau |
| 3, 4, 5 | Báo admin, xem log trên NAS |

### Ví dụ C#

```csharp
var queueDir = "/app/domain-queue"; // đường dẫn mount trong container
var domain = input.Trim().ToLowerInvariant();

// gửi yêu cầu
File.Create(Path.Combine(queueDir, $"{domain}.add")).Dispose();

// đọc kết quả (background job / polling)
var resultPath = Path.Combine(queueDir, $"{domain}.result");
if (File.Exists(resultPath))
{
    var r = JsonSerializer.Deserialize<DomainResult>(File.ReadAllText(resultPath),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    // cập nhật DB theo r.Ok / r.Code / r.Message
    File.Delete(resultPath);
}

public record DomainResult(string Domain, string Action, bool Ok, int Code, string Message, string At);
```

### Khuyến nghị cho app

- Kiểm tra DNS ngay trong app trước khi tạo file `.add` (domain phải resolve về cùng IP với domain gốc), tránh tốn một lượt xử lý.
- Mỗi domain mất khoảng 1–2 phút (DSM phải reload nginx); các domain được xử lý lần lượt.
- Validate domain trước khi ghi file: chỉ chữ thường, số, dấu `-` và `.`.

---

## 6. Hướng dẫn khách hàng cấu hình DNS

Khuyến nghị dùng **CNAME** trỏ về domain gốc:

```
crm.congty.vn.   CNAME   billhub.sunmedia.vn.
```

- Nếu không dùng được CNAME, dùng bản ghi A trỏ về IP public của NAS.
- **Không** để bản ghi AAAA (IPv6) trỏ chỗ khác: Let's Encrypt ưu tiên IPv6 nên xác thực sẽ fail.
- Đợi DNS cập nhật (thường vài phút, có thể lâu hơn) rồi mới bấm thêm domain.

---

## 7. Gia hạn chứng chỉ

- Chứng chỉ Let's Encrypt có hạn 90 ngày. DSM **tự gia hạn** các chứng chỉ do nó quản lý, với điều kiện port 80 vẫn mở và DNS của khách vẫn trỏ về NAS.
- Lệnh `sync` (chạy hằng ngày):
  - Nếu DSM đã gia hạn nhưng rule chưa nhận chứng chỉ mới → copy chứng chỉ vào rule và reload nginx.
  - Nếu chứng chỉ còn dưới `RENEW_WARN_DAYS` ngày → ghi `WARN` vào log.
- Nên kiểm tra log định kỳ: `grep WARN /var/log/nas-domain.log`.

---

## 8. Xử lý sự cố

| Triệu chứng | Nguyên nhân / cách xử lý |
|---|---|
| `code 2`: domain chưa có bản ghi A/CNAME hoặc trỏ sai | Khách chưa cấu hình DNS hoặc DNS chưa cập nhật. Kiểm tra: `curl -s "https://dns.google/resolve?name=crm.congty.vn&type=A"` |
| `code 2`: có AAAA không trỏ về NAS | Nhờ khách xóa bản ghi AAAA |
| `Server is not reachable` ngay sau `finalize` | Đang dùng `--staging` → chạy lại không có `--staging`. Nếu xảy ra ở môi trường thật, xem mục 9 |
| `Server is not reachable` ngay từ đầu | NAS không ra được internet / DNS của NAS lỗi / IPv6 trên NAS cấu hình sai |
| Xác thực HTTP-01 fail (Invalid response) | Port 80 chưa forward về NAS, hoặc có rule Reverse Proxy port 80 cho domain đó chặn `/.well-known/acme-challenge` |
| `Incorrect port map rules result` trong log | DSM thử mở port qua UPnP — vô hại nếu đã forward port thủ công |
| `code 3`: không tìm thấy rule mẫu | Rule `TEMPLATE_FQDN` trên port 443 bị xóa/đổi tên — sửa biến hoặc tạo lại rule |
| `code 5`: cert chưa được phục vụ | Kiểm tra Control Panel > Security > Certificate > Settings xem rule đã chọn đúng cert chưa; chạy `sync` |
| Script treo / không chạy | Lock cũ còn sót: `rmdir /tmp/billhub-domain.lock` (lock tự hết hạn sau 30 phút) |

### Khi API DSM thay đổi sau cập nhật

1. Mở DSM bằng trình duyệt, bấm **F12** → tab **Network**.
2. Thao tác bằng tay bước bị lỗi (tạo Reverse Proxy, gán cert…).
3. Xem request `entry.cgi` → Form Data để đối chiếu tên API, method, tham số với script.

### Kiểm tra nhanh

```bash
# cert đang phục vụ cho domain
echo | openssl s_client -connect 127.0.0.1:443 -servername crm.congty.vn 2>/dev/null \
  | openssl x509 -noout -subject -issuer -enddate

# danh sách rule Reverse Proxy
synowebapi --exec api=SYNO.Core.AppPortal.ReverseProxy method=list version=1 2>/dev/null | jq '.data.entries[] | {fqdn: .frontend.fqdn, UUID, description}'

# danh sách cert và rule đang dùng
synowebapi --exec api=SYNO.Core.Certificate.CRT method=list version=1 2>/dev/null | jq '.data.certificates[] | {id, desc, services: [.services[]?.display_name]}'

# log
tail -n 100 /var/log/nas-domain.log
```

---

## 9. Lưu ý quan trọng

- **Không tạo rule Reverse Proxy port 80** cho domain khách: sẽ chặn xác thực Let's Encrypt và làm hỏng gia hạn. Chuyển HTTP → HTTPS nên xử lý trong app.
- **Mỗi lần tạo/xóa rule, DSM reload nginx cho tất cả domain** (mất khoảng 30–60 giây). Script có lock để không chạy song song.
- **`remove` không xóa chứng chỉ.** Xóa tay ở Control Panel > Security > Certificate để DSM ngừng gia hạn chứng chỉ của domain đã bỏ.
- **Giới hạn của Let's Encrypt**: số lần xác thực thất bại trên mỗi domain mỗi giờ có giới hạn; đừng thử lại liên tục khi DNS chưa đúng.
- **Mỗi domain một chứng chỉ riêng** (không gom chung SAN): domain nào lỗi thì chỉ domain đó bị ảnh hưởng.
- **Rủi ro dài hạn**: nếu Let's Encrypt bật cơ chế hoàn tất đơn bất đồng bộ ở môi trường thật, `syno-letsencrypt` sẽ fail giống lỗi staging (cả xin mới lẫn gia hạn). Khi đó cần chuyển sang client ACME khác (acme.sh, hoặc Caddy với on-demand TLS đứng trước app).
- Script tự sao lưu file `INFO` trước khi sửa: `/usr/syno/etc/certificate/_archive/INFO.bak.<timestamp>`.

---

## 10. Vị trí file

| Đường dẫn | Nội dung |
|---|---|
| `/volume1/scripts/billhub-domain.sh` | Script chính |
| `/volume1/docker/billhub/domain-queue/` | Hàng đợi `.add` / `.remove` / `.result` |
| `/var/log/nas-domain.log` | Log của script (kèm output đầy đủ của `syno-letsencrypt`) |
| `/usr/syno/etc/certificate/_archive/<id>/` | Chứng chỉ do DSM quản lý |
| `/usr/syno/etc/certificate/_archive/INFO` | Danh sách cert và service đang dùng |
| `/usr/syno/etc/certificate/ReverseProxy/<UUID>/` | Cert đã triển khai cho từng rule Reverse Proxy |
