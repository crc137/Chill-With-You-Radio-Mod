<div align="center">
  <a href="https://github.com/coonlink">
    <img width="90px" src="logo.png" alt="Logo" />
  </a>
  <h1>Chill with You : Lo-Fi Story — Мод «Радио»</h1>

[![English](https://img.shields.io/badge/lang-English%20🇺🇸-white)](README.md)
[![Русский](https://img.shields.io/badge/язык-Русский%20🇷🇺-white)](README.ru.md)

<img alt="last-commit" src="https://img.shields.io/github/last-commit/crc137/Chill-With-You-Radio-Mod?style=flat&amp;logo=git&amp;logoColor=white&amp;color=0080ff" style="margin: 0px 2px;">
<img alt="repo-top-language" src="https://img.shields.io/github/languages/top/crc137/Chill-With-You-Radio-Mod?style=flat&amp;color=0080ff" style="margin: 0px 2px;">
<img alt="repo-language-count" src="https://img.shields.io/github/languages/count/crc137/Chill-With-You-Radio-Mod?style=flat&amp;color=0080ff" style="margin: 0px 2px;">
<img alt="version" src="https://img.shields.io/badge/version-26.1.3-blue" style="margin: 0px 2px;">
<img alt="status" src="https://img.shields.io/badge/status-STABLE-green" style="margin: 0px 2px;">
</div>

<br />

<div align="center">
  <p>Добавляет в игру <b>рабочее интернет-радио</b>. Переключение станций — без заикания.</p>
</div>

## Требования

Чтобы радио заработало, нужно **всё** из списка:

1. Игра **Chill with You : Lo-Fi Story** (любая версия, Steam), запущенная один раз.
2. **BepInEx 5.x**, установленный в папку игры
   → `...Chill with You Lo-Fi Story/BepInEx/`
   (установщик ставит его автоматически, если его нет — например, после переустановки игры).
3. Файлы плагина `RadioStreamPlugin.dll` и `NLayer.dll` (этот мод).
4. Интернет (радио играет вживую из сети).


## Скачать

Готовый к запуску zip выложен **только** в GitHub Releases:

👉 [github.com/crc137/Chill-With-You-Radio-Mod/releases](https://github.com/crc137/Chill-With-You-Radio-Mod/releases)

Берите `ChillWithYou-RadioMod-v26.1.3-Linux-Windows.zip`. В самом репозитории —
только исходники, установщики и списки станций, без бинарников.



## Как установить (игроку, сборка не нужна)

**Вариант A — установщик в один клик (рекомендую)**

Положите `install.sh`, `install.bat`, `RadioStreamPlugin.dll`, `NLayer.dll` и `radiostations.txt` в одну папку и запустите установщик для своей ОС:

- **Windows:** двойной клик по `install.bat`
- **Linux / Steam Deck:** `./install.sh`

Установщик найдёт игру в **любой Steam-библиотеке** (включая нестандартные пути и внешние диски), сам поставит BepInEx, если его нет (например, после того как игру удалили и скачали заново), и скопирует мод в `BepInEx/plugins`. Если игра не нашлась — спросит путь вручную.

**Вариант B — вручную**

1. Установите игру через Steam и **один раз** запустите её, чтобы создались папки.
2. Положите **BepInEx 5.x** в папку игры:
   - Windows: `Chill with You Lo-Fi Story/BepInEx/`
   - Steam Deck / Linux (flatpak):
     `~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Chill with You Lo-Fi Story/BepInEx/`
3. Скопируйте **все файлы** из скачанного релизного zip в папку **plugins**:
   ```
   BepInEx/plugins/RadioStreamPlugin.dll     ← сам мод
   BepInEx/plugins/NLayer.dll                ← декодер MP3 (обязателен)
   BepInEx/plugins/radiostations.txt         ← список станций
   ```
4. Запустите игру, откройте меню музыки и переключайте станции клавишами **J / K**.

### Проверка станций (по желанию)

Радиостанции в интернете живут недолго, а декодер мода умеет только MP3/MPEG.
В архиве есть валидатор: он опрашивает все станции так же, как мод, и оставляет
только реально играющие. Запустите **до** установки своего списка:

- **Windows:** двойной клик по `checkstations.bat`
- **Linux / Steam Deck:** `./checkstations.sh`

Результат — `radiostations.checked.txt` (рабочие) и `radiostations.checked.skipped.txt`
(отброшенные с причиной). Скопируйте рабочий список в `BepInEx/plugins/radiostations.txt`.
Если станция падает с ошибкой TLS — её не поддержит рантайм игры, выбросьте.

Если файла со станциями нет — плагин использует стандартные. Если после переустановки игры пропал BepInEx — запустите `install.sh` / `install.bat`, он поставит его автоматически.



## Как настроить станции

`radiostations.txt` — по одной станции на строку, формат (сначала URL, через `|`):

```
URL|Название|Автор|Описание
```

Пример:

```
http://example.com/lofi.mp3|Lo-Fi Beats
http://example.com/jazz.pls|Jazz Radio
```

Измените файл и **перезапустите игру** — новые станции подхватятся. Радио играет только если адрес доступен и отдаёт MP3/MPEG — декодер (NLayer) умеет только MP3/MPEG (не AAC/OGG). Лучше вставлять прямые ссылки на `.mp3`.



## Управление

| Клавиша | Действие |
|---|---|
| **J** | Предыдущая станция |
| **K** | Следующая станция |


## Если не работает

- **Нет радио в меню / нет музыки** → проверьте, что BepInEx реально установлен (в папке игры должна быть `BepInEx/core`), а в `BepInEx/plugins` лежат `RadioStreamPlugin.dll` **и** `NLayer.dll`. Если игру только что переустановили — запустите установщик заново, он восстановит BepInEx и мод.
- **Станция не играет** → адрес недоступен или формат не поддерживается. Замените её в `radiostations.txt` на прямую ссылку потока.
- **Не видно консоли BepInEx** → в `BepInEx/config/BepInEx.cfg` включите `[Logging.Console] Enabled = true`.

## Сборка из исходников (для разработчиков)

Нужен .NET SDK (>= 6) и скрипт `build.sh` (сам находит игру на Linux / Windows, собирает и копирует DLL). Запуск:

```bash
./build.sh
```
