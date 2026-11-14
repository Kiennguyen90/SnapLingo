# Deploy SnapLingo API lên Synology

Workflow `.github/workflows/deploy-synology.yml` deploy API lên self-hosted runner có
labels `self-hosted` và `synology` khi repository variable `DEPLOY_ENABLED=true`
sau khi job `build` publish image hoàn tất trên branch `master`.

## Chuẩn bị thư mục deploy

Mặc định workflow dùng:

```text
/volume1/docker/snaplingo/app
```

Có thể đổi bằng repository variable `DEPLOY_DIR`.

```bash
sudo mkdir -p /volume1/docker/snaplingo/app
sudo cp deploy/synology/.env.example /volume1/docker/snaplingo/app/.env
sudo chmod 600 /volume1/docker/snaplingo/app/.env
```

Điền các giá trị production trong `.env`:

- `POSTGRES_PASSWORD`
- `JWT_SIGNING_KEY`
- `STORAGE_SIGNING_KEY`
- `STORAGE_PUBLIC_BASE_URL`

PostgreSQL được chạy bằng service `postgres` trong compose và dữ liệu được lưu
ở Docker volume `postgres_data`. API lưu media trong volume `media_data`.

## Cài self-hosted runner

Đăng ký runner repository với labels `synology`, sau đó chạy runner bằng
[`runner/docker-compose.yml`](runner/docker-compose.yml). Runner cần mount
Docker socket và cùng đường dẫn `DEPLOY_DIR` ở bên trong lẫn bên ngoài
container.

## Quy trình deploy

Khi workflow được kích hoạt:

1. Runner kiểm tra Docker Compose và `.env`.
2. Workflow copy `docker-compose.yml` mới vào `DEPLOY_DIR`.
3. Runner đăng nhập GHCR, pull image `snaplingo-api`.
4. PostgreSQL được khởi động trước; API migrate database khi
   `DATABASE_MIGRATE_ON_STARTUP=true`.
5. Compose chờ healthcheck `/health` của API tối đa 5 phút.

API được publish mặc định ở port `5082`, phù hợp để DSM Reverse Proxy chuyển
HTTPS vào `http://localhost:5082`.
