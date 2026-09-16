# Развёртывание на сервере

Боевой сайт — `https://teriberka-kray.ru`. Стек тот же, что локально, но поднимается
с прод-оверлеем: `docker-compose.prod.yml` включает `Production` у всех .NET-сервисов
(это выключает `/dev/token` и Swagger), подставляет секреты из `.env`, отдаёт nginx
TLS-конфиг и ограничивает рост docker-логов.

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
```

Команда длинная и нужна каждый раз — на сервере удобно завести алиас:
`alias dc='docker compose -f /opt/teriberka/docker-compose.yml -f /opt/teriberka/docker-compose.prod.yml'`.

## Что где лежит

| Что | Где |
|---|---|
| Код и compose-файлы | `/opt/teriberka` |
| Секреты (`.env`) | `/opt/teriberka/.env` — **не в git**, при переустановке восстанавливается руками |
| Сертификат | `/opt/teriberka/certs/{fullchain.pem,privkey.pem}` — **не в git** |
| База | том `db_volume` |
| Фото | том `minio_volume` — **единственная копия**, бэкапов пока нет |

## Первая установка

1. Firewall: наружу только 22, 80, 443. Порты 5000/5012/5013/5432/9001 в compose
   привязаны к `127.0.0.1` и снаружи недоступны — так и должно быть: users и chat
   никого не авторизуют и доверяют вызывающему.
2. Swap. Сервер — 2 vCPU / 2 ГБ, и памяти не хватает не сайту, а **сборке**:
   `dotnet publish` держит около гигабайта на проект. Без swap первая же сборка
   упирается в OOM.

   ```bash
   fallocate -l 4G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
   echo '/swapfile none swap sw 0 0' >> /etc/fstab      # чтобы пережил перезагрузку
   ```

3. Код: `git archive --format=tar.gz -o teriberka.tar.gz HEAD` на машине разработчика,
   `scp` на сервер, распаковать в `/opt/teriberka`. Именно `git archive`, а не `zip`
   рабочего каталога: он берёт версионированное состояние и сохраняет LF в `.sh`
   (с CRLF `init-db.sh` не запустится — `\r` попадёт в shebang).
4. `.env` по образцу `.env.example`. Пароли — `openssl rand -hex 32`
   (base64 не годится: `=` в пароле ломает строку подключения Postgres).
   `POSTGRES_PASSWORD` задать **до первого старта** — на уже созданном томе
   он молча игнорируется.
5. Сертификат — см. ниже.
6. `docker compose -f docker-compose.yml -f docker-compose.prod.yml config` — сверить,
   что подставилось, и только потом `up -d --build`.

## Сертификат

Сертификат коммерческий (reg.ru), выпускается на год, автопродления нет.
**Приватный ключ reg.ru отдаёт один раз, в момент выпуска, и больше не показывает** —
потеряли ключ, значит нужно переиздание.

Из письма нужны два файла: сертификат домена и промежуточный. Корневой не нужен
(браузеры ему и так доверяют), CSR после выпуска не нужен вовсе.

```bash
cat certificate.crt intermediate.crt > fullchain.pem   # порядок важен: домен, потом промежуточный
cp private.key privkey.pem
chmod 600 privkey.pem
```

Перед установкой три проверки:

```bash
# ключ подходит к сертификату — хэши должны совпасть
openssl x509 -noout -modulus -in fullchain.pem | openssl md5
openssl rsa  -noout -modulus -in privkey.pem   | openssl md5

# ключ не под паролем: BEGIN ENCRYPTED PRIVATE KEY => nginx не стартует без ssl_password_file
head -1 privkey.pem

# какие имена покрывает и до какого числа
openssl x509 -in fullchain.pem -noout -text | grep -A1 "Subject Alternative Name"
openssl x509 -in fullchain.pem -noout -dates
```

Если сертификат покрывает только голый домен — в `.env` оставить `SITE_DOMAIN`
с одним именем. Если покрывает и `www` — перечислить оба **через пробел**
(формат `server_name` у nginx), редирект `www` → apex конфиг сделает сам.

**Продление** (напоминание за месяц до даты из `-dates`): переиздать в панели reg.ru,
**сразу сохранить новый `private.key`**, заменить оба файла в `certs`, затем
`docker compose ... exec nginx nginx -s reload` — перезапуск контейнера не нужен.

HSTS в конфиге намеренно начинается с `max-age=300`. Через несколько дней стабильной
работы поднять до `31536000` в `infra/nginx/templates.tls/default.conf.template`
(два места: блок `server` и блок `location /media`) и перезагрузить nginx. Раньше
времени не поднимать: пока срок маленький, ошибку в сертификате ещё можно откатить.

## Капча

Форма заявки закрыта невидимой Яндекс SmartCaptcha. Она включается **парой** ключей;
половина пары = капча выключена целиком (с warning'ом в логе), и форма работает как
раньше — сайт не падает, но и не защищён.

- `SMARTCAPTCHA_CLIENT_KEY` — публичный, лежит дефолтом в compose, трогать не нужно.
- `SMARTCAPTCHA_SERVER_KEY` — **секрет, только в `.env` на сервере**. Без него защиты нет.

**Домен нужно разрешить в консоли Yandex Cloud** (SmartCaptcha → капча → список
доменов): добавить `teriberka-kray.ru` и `www.teriberka-kray.ru`. Пока там только
локальный адрес, виджет на проде будет отказывать — а поскольку без токена заявка
не отправляется, форма перестанет работать совсем. Это первое, что нужно проверить
после запуска, и проверять именно в браузере: в логах такой отказ не виден.

**Серверу нужен исходящий HTTPS** к `smartcaptcha.cloud.yandex.ru`: токен проверяется
с бэкенда. Если исходящие соединения закрыты, каждая заявка будет ждать таймаут 4 с
и проходить без проверки (fail-open — сознательное решение: сбой Яндекса не должен
терять заявки, но с наглухо закрытым фаерволом это тихо выключает капчу).

С включённой капчей **заявку нельзя отправить без JavaScript** — токен собирает
скрипт. Это осознанное изменение от 28.08.2026, а не регресс.

## Обновление сайта

```bash
# на машине разработчика: архив и список файлов, удалённых с прошлой выкатки
git archive --format=tar.gz -o teriberka.tar.gz HEAD
prev=$(ssh user@server cat /opt/teriberka/.deployed-commit)
git diff --name-only --no-renames --diff-filter=D "$prev" HEAD > deleted.txt
scp teriberka.tar.gz deleted.txt user@server:/opt/teriberka/

# на сервере: распаковать, убрать удалённое, собрать
cd /opt/teriberka && tar xzf teriberka.tar.gz
while IFS= read -r f; do [ -f "$f" ] && rm -- "$f"; done < deleted.txt
rm deleted.txt teriberka.tar.gz
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build

# на машине разработчика, когда сайт проверен: запомнить выкаченный коммит
git rev-parse HEAD | ssh user@server 'cat > /opt/teriberka/.deployed-commit'
```

`.env` и `certs/` распаковка не затрагивает — они не входят в архив.

**Удалённые из репозитория файлы распаковка не удаляет.** `tar xzf` только добавляет
и перезаписывает: убранный из git файл остаётся лежать в `/opt/teriberka`, попадает
в образ при сборке и продолжает раздаваться сайтом. Это не просто мусор в образе —
фотолента гостей показывает всё, что лежит в `img/guests/`, так что удалённый кадр
остался бы на странице. Отсюда шаги с `deleted.txt` выше (16.09.2026 так вручную
убирали 51 старый кадр программы дня и галерей мест).

- `--no-renames` обязателен: без него переименование git показывает как `R`, и старый
  путь в список удалённых не попадает.
- Если `.deployed-commit` на сервере нет, взять прошлый выкаченный коммит из истории
  (на 16.09.2026 на сервере `115104a`) и записать его командой из последнего шага.
- Файл запоминается после проверки сайта, а не сразу: если выкатку пришлось
  повторять, следующий список удалённых посчитается от той же точки.

**Сборка на этом сервере — по одному сервису.** `docker compose build` по умолчанию
собирает сервисы параллельно, а четыре одновременных `dotnet publish` на 2 ГБ кладут
машину даже со swap (лимиты памяти из `docker-compose.prod.yml` тут не работают:
сборка идёт в buildkit, мимо контейнеров):

```bash
for svc in api-users api-chat api-gateway ui-public; do
  docker compose ... build "$svc" || break
done
docker compose ... up -d
```

Именно цикл, а не `build` со списком сервисов: список compose раскладывает по
параллельным задачам, и ограничить их числом одной переменной окружения
надёжно не получается.

Диск (40 ГБ) со временем забивает кэш сборки — `docker builder prune -f` и
`docker image prune -f` раз в несколько деплоев. Проверять `docker system df`.

Альтернатива, если сборка на сервере надоест: собирать образы на своей машине,
`docker save` → `scp` → `docker load`. Дольше по сети, но сервер не занят.

Правка только конфига nginx (шаблон монтируется с диска, образ пересобирать не нужно):
`docker compose ... up -d --force-recreate nginx`.

Изменения в контрактах требуют пересборки того сервиса, который их использует,
причём именно `--force-recreate`: `--build` без него пересобирает образ, но
оставляет работать старый контейнер.

## Telegram через туннель

С этого сервера Telegram не отвечает — ни `api.telegram.org`, ни дата-центры
(проверено 16.09.2026). Поэтому users (отбивка заявок в канал) и chat (бот) ходят
в Telegram через socks5-прокси: туннель VLESS + XHTTP + Reality на финский VPS.

- Туннель **не в этом репозитории**: отдельный compose-проект `/opt/tg-relay`
  (контейнер `tg-relay-xray`, конфиг с ключами есть только на сервере), выход —
  `/opt/proxy` на финском VPS (Xray + Caddy). Там же, в `/opt/tg-relay`, лежит
  выключенный MTProto-прокси для телефонов (профиль `mtproto`): на мобильном
  интернете его режут, включать не нужно.
- Xray подключён к сети `teriberka_default` под именем **`tg-proxy`**, socks без
  пароля на порту 1080, наружу не опубликован.
- В `.env`: `TG_PROXY_URL=socks5://tg-proxy:1080`.

Проверить туннель:

```bash
docker run --rm --network teriberka_default curlimages/curl:8.16.0 -s -o /dev/null \
  -w '%{http_code}\n' -x socks5h://tg-proxy:1080 https://api.telegram.org   # ждём 302
```

**Ловушка:** `docker compose down` у проекта teriberka удаляет сеть
`teriberka_default`, и xray из неё выпадает. После `up` вернуть его:
`cd /opt/tg-relay && docker compose up -d`. Обычный `up -d --build` сеть не трогает.
Туннель лёг — сайт работает, в логах users/chat таймауты Telegram; заявки
дождутся восстановления в БД (в канал уйдут, если им меньше суток).

### Включение отбивки заявок

1. Бот в @BotFather → токен в `.env` (`TG_BOT_TOKEN`), там же `TG_PROXY_URL`;
   `up -d` пересоздаст api-users и api-chat.
2. Приватный канал (в сообщениях имя и телефон) → бот администратором с правом
   публикации → в `docker logs teriberka-api-chat-1` появится строка
   «Бота добавили в чат -100…».
3. Этот id → `TG_APPLICATIONS_CHAT_ID` в `.env` → `up -d`. Проверка — заявка с сайта:
   через ~10 с сообщение в канале, в логе users «Заявка … отправлена в Telegram-канал».

## Страница заявок

`https://teriberka-kray.ru/manager` — заявки для менеджеров (вход по общему паролю).
Нужны два ключа в `.env`:

```bash
# пароль менеджеров — случайные буквы и цифры от 8 символов, без $ # и кавычек;
# его и отдать владельцу (в менеджер паролей)
openssl rand -hex 8
# служебный токен: ключ и issuer — из окружения работающего шлюза (прод-оверлей их
# переопределяет, на бою issuer — https://teriberka-kray.ru, а не localhost)
env=$(docker inspect -f '{{range .Config.Env}}{{println .}}{{end}}' teriberka-api-gateway-1)
python3 tools/make-api-token.py "$(printf '%s
' "$env" | sed -n 's/^JWT_KEY=//p')"   "$(printf '%s
' "$env" | sed -n 's/^JWT_ISSUER=//p')" 3
```

Проверить токен до записи в `.env` (заголовок через stdin — токен не попадает в список
процессов; `curlimages/curl` работает не от root и файлы из `/root` не читает):
`printf 'Authorization: Bearer %s
' "$TOKEN" | docker run --rm -i --network teriberka_default curlimages/curl:8.16.0 -s -o /dev/null -w '%{http_code}' -H @- http://api-gateway:8080/api/admin/application/list?limit=1` —
ждём `200` (без заголовка — `401`).

`MANAGER_PASSWORD=…` и `API_TOKEN=…` в `.env`, затем `up -d ui-public`. Проверка: `/manager`
отправляет на вход, после входа видны заявки. «Не удалось получить заявки» — токен не
подходит шлюзу (другой `JWT_KEY` или истёк срок). Сменить пароль (ушёл менеджер) —
поменять `MANAGER_PASSWORD` и `up -d ui-public`: все старые входы перестанут действовать.
Токен выпущен на 3 года — дата истечения в самом токене (`exp`), перевыпустить заранее.

## Наблюдение

```bash
docker compose ... ps
docker compose ... logs -f ui-public          # сайт
docker compose ... logs -f api-chat           # чат и Telegram-бот
docker compose ... exec nginx nginx -t        # проверить конфиг перед reload
```

Заявки (Telegram-бот их пока не читает):

```bash
docker compose ... exec postgres psql -U postgres -d platform \
  -c 'select "Title", "Phone", "Route", "Audit_CreatedAt" from users."Applications" where not "IsDeleted" order by "Audit_CreatedAt" desc limit 20'
```

Консоль MinIO наружу не публикуется никогда — только через туннель:
`ssh -L 9001:127.0.0.1:9001 user@server`, дальше `http://localhost:9001`.

## Чего ещё нет

- **Бэкапов.** Ни базы, ни фото. Том `minio_volume` уже терялся при перестроении
  compose, а это единственная копия загруженных кадров.
- **Автопродления сертификата** — раз в год руками (см. выше).
- **Группы гидов для чата.** Бот `@trbrkray_bot` работает (16.09.2026, токен в `.env`,
  через туннель), заявки уходят в канал, ссылка на бота на сайте настоящая. Но
  `TG_ADMIN_CHAT_ID` пуст: чат на сайте сохраняет переписку, а в Telegram она не
  уходит — **сообщения посетителей сейчас никто не видит**. Завести приватную группу,
  добавить бота, id из лога chat — в `TG_ADMIN_CHAT_ID`, `up -d`.
- **Метрики.** OTLP выключен, логи живут только в docker (10 МБ × 5 файлов на сервис).
