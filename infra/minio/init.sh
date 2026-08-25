#!/bin/sh
# Разовая настройка хранилища медиа. Запускается при каждом `docker compose up`,
# поэтому обязана быть идемпотентной: всё, что уже создано, молча пропускается.
#
# Политики пишем здесь через heredoc, а не отдельными файлами с подстановкой:
# в образе minio/mc нет ни sed, ни awk — только sh и cat.
set -eu

mc alias set local "http://minio:9000" "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD"

# 1. Бакет
mc mb --ignore-existing "local/$MEDIA_BUCKET"

# 2. Анонимное чтение — только GetObject и только на префикс hero/.
#    Намеренно НЕ используем `mc anonymous set download`: в части версий mc она
#    добавляет ещё и s3:ListBucket, то есть открывает перечисление бакета наружу.
cat > /tmp/public-read.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "AWS": ["*"] },
      "Action": ["s3:GetObject"],
      "Resource": ["arn:aws:s3:::$MEDIA_BUCKET/hero/*"]
    }
  ]
}
EOF
mc anonymous set-json /tmp/public-read.json "local/$MEDIA_BUCKET"

# 3. Учётка сайта: только чтение и листинг. Root-креденшелы в UI класть нельзя —
#    компрометация процесса UI дала бы полный доступ к хранилищу.
cat > /tmp/ui-read.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["s3:ListBucket"],
      "Resource": ["arn:aws:s3:::$MEDIA_BUCKET"]
    },
    {
      "Effect": "Allow",
      "Action": ["s3:GetObject"],
      "Resource": ["arn:aws:s3:::$MEDIA_BUCKET/*"]
    }
  ]
}
EOF
mc admin policy create local media-read /tmp/ui-read.json 2>/dev/null \
  || mc admin policy update local media-read /tmp/ui-read.json 2>/dev/null \
  || true
mc admin user add local "$MEDIA_ACCESS_KEY" "$MEDIA_SECRET_KEY" 2>/dev/null || true
mc admin policy attach local media-read --user "$MEDIA_ACCESS_KEY" 2>/dev/null || true

# 4. Заготовка префикса, чтобы владелец увидел в консоли готовую папку
#    и не гадал, куда класть файлы.
mc cp /dev/null "local/$MEDIA_BUCKET/hero/.keep" 2>/dev/null || true

echo "minio-init: ok"
