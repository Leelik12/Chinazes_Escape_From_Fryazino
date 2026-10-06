# RacingProject — «Chinazes: Escape From Fryazino»

Кооперативная VR-игра для двух игроков в постапокалиптическом сеттинге заброшенного советского города. Один игрок — **водитель** (руль, педали, мехатронная платформа 2DOF), второй — **стрелок** (VR-контроллеры, платформа FutuRift). Задача — уйти от вражеских машин с турелями, отстреливаясь на ходу. Проект разработан в рамках курсовой работы «Разработка VR-игры с использованием мехатронных устройств».

## Возможности

- Главное меню в VR: «Начать игру», «Выйти из игры», страница советов, настройки громкости двигателя и музыки (`AudioMixer`).
- Сетевая игра на двоих через Photon PUN 2: первый вошедший создаёт комнату на 2 игроков и становится мастер-клиентом (водителем), второй подключается и становится стрелком.
- Управление автомобилем на `WheelCollider`: механическая коробка передач (задняя, нейтраль, 1–6) с переключением через сцепление, газ/тормоз, ограничение скорости на каждой передаче, вывод скорости и передачи на UI, звук двигателя, толчок машины крестовиной руля, если она застряла, магнитола.
- Поддержка руля Logitech G29 (через Logitech G SDK и Input System).
- Синхронизация по сети: позиция и поворот машины, поворот руля, IK рук и головы водителя, оружие и руки стрелка.
- Противники: машины с ИИ (преследование игрока) и турелями, стреляющими по машине игроков; спавн через `EnemyManager`.
- Стрельба стрелка из пистолета (XR Interaction Toolkit): захват оружия, затвор, магазины, эффекты (вспышка, искры, декали).
- Прочность машины игроков и врагов с отображением на UI.
- Телеметрия на мехатронные платформы: 2DOF (через memory-mapped file) и FutuRift (UDP/COM-порт).
- Тактильная отдача bHaptics (жилет TactSuit, нарукавники Tactosy).

## Стек

- **Unity 6** — `6000.0.41f1`, рендер URP (`com.unity.render-pipelines.universal` 17.0.4).
- **C#**, скрипты в `Assets/Scripts`.
- **XR**: XR Interaction Toolkit 3.0.8, OpenXR 1.14.3, XR Management. Целевой шлем — Oculus (Meta) Quest 2.
- **Сеть**: Photon PUN 2 (`Assets/Photon`). В проекте также лежит Mirror (по `.csproj`), но игровые скрипты используют Photon.
- **Ввод**: Input System 1.13.1, Logitech G29 (`Assets/Plugins/LogitechG29`).
- **Мехатроника**: `Assets/Plugins/2DOF`, `Assets/Plugins/Futurift`.
- **Тактильность**: bHaptics SDK2 (`Assets/Bhaptics`).
- **Окружение**: Road Architect (дороги), Terrain Tools, Cinemachine, AI Navigation, ассеты из `Assets/Content` (здания, машины, деревья, оружие).

## Требования

- Unity Hub и Unity Editor **6000.0.41f1** (модули Windows Build Support, при сборке под Quest — Android Build Support).
- Windows (Logitech G SDK, 2DOF и FutuRift работают под Windows).
- VR-шлем с OpenXR (Quest 2 через Link/Air Link или другой).
- Для полного режима: руль Logitech G29, платформы 2DOF и FutuRift с их ПО, устройства bHaptics с приложением bHaptics Player.
- Учётная запись Photon и App ID PUN (настраивается в `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset`).

## Установка и запуск

1. Клонировать репозиторий.
2. Открыть папку проекта через Unity Hub (версия 6000.0.41f1). Пакеты из `Packages/manifest.json` подтянутся автоматически, `Library/` пересоздастся.
3. Проверить App ID Photon в `PhotonServerSettings` (Window → Photon Unity Networking → Highlight Server Settings).
4. Открыть сцену `Assets/Scenes/SovietCity.unity` — единственная сцена в Build Settings.
5. Нажать Play. Первый запустившийся клиент становится водителем, второй — стрелком. Для теста вдвоём нужны две копии (сборка + редактор или две сборки).

### Управление водителем без руля (отладка)

Машиной управляет `CarControllerSample` (`Assets/Scripts/Car/CarControllerSample.cs`) через `InputControllerReader` из `Assets/Plugins/LogitechG29`. В схеме ввода `InputController.inputactions` действия руля продублированы на клавиатуре:

| Клавиша | Действие |
|---|---|
| `W` / `S` | газ / тормоз |
| `A` / `D` | руль |
| `F` | сцепление |
| `1`–`6` | передачи 1–6 |
| `7` | задняя передача |

Передача переключается только при выжатом сцеплении и соответствует нажатой клавише: если отпустить цифру, пока зажата `F`, включится нейтраль. Поэтому сначала отпускайте `F`, потом цифру.

## Сборка

File → Build Profiles → выбрать платформу (Windows для ПК-VR или Android для автономного Quest) → Build. Сцена в сборке: `Assets/Scenes/SovietCity.unity`. Работа платформ 2DOF/FutuRift и руля G29 возможна только в Windows-сборке.

## Тесты

Автотестов в проекте нет (пакет `com.unity.test-framework` подключён, но тесты не написаны).

## Структура проекта

```
Assets/
  Scenes/SovietCity.unity      — основная сцена (меню + игровая локация)
  Scripts/
    Car/                       — CarControllerSample (управление машиной), CarHapticsController (bHaptics), Magnitola
    Enemy/                     — EnemyCarController (ИИ), EnemyGun (турель), EnemyHealth, EnemyManager (спавн)
    Network/                   — PhotonLauncher (подключение), RoomController (роли, старт и перезапуск раунда)
    PlayerBody/                — IK головы и рук, сетевое следование прокси-точек
    Turret/                    — VRGun (пистолет стрелка), захват и возврат оружия
    Management/                — MenuManager (меню, громкость), StaticHolder, расстановка препятствий на террейне
    Telemetry/                 — CarTelemetryHandler (2DOF), FuturiftTelemetryHandler (FutuRift)
    PlayerHealth.cs, BarGradient.cs, HandAnimationSync.cs
    ShotEffects.cs             — общие эффекты выстрела (вспышка, попадание) для оружия игрока и врагов
  Plugins/
    2DOF/                      — отправка телеметрии на платформу 2DOF
    Futurift/                  — контроллер FutuRift (UDP 127.0.0.1:6065 или COM-порт)
    LogitechG29/               — поддержка руля Logitech G29
  Resources/                   — префабы для Photon: машина игроков, враги, турель, пистолет, эффекты, XR Origin
  Photon/                      — Photon PUN 2
  Bhaptics/                    — bHaptics SDK2
  Content/                     — сторонние ассеты (здания, машины, деревья, оружие, текстуры)
  RoadArchitect/               — инструмент построения дорог
  Settings/                    — ассеты URP (PC и Mobile)
  XR/, XRI/                    — настройки XR и XR Interaction Toolkit
Packages/                      — манифест пакетов Unity
ProjectSettings/               — настройки проекта
```

## Конфигурация

- **Photon**: `PhotonLauncher` — имя комнаты `Room1`, максимум 2 игрока, `SendRate` и `SerializationRate` = 120.
- **FutuRift**: параметры подключения в `Assets/Plugins/Futurift/Options` (`UdpOptions`: `127.0.0.1:6065`, `ComPortOptions`: COM3).
- **2DOF**: `CarTelemetryHandler` (`Assets/Scripts/Telemetry`) считает наклоны и ускорения машины и передаёт их через memory-mapped file `2DOFMemoryDataGrabber` с интервалом 20 мс (`Assets/Plugins/2DOF/SendingData.cs`); их забирает ПО платформы.
- **Раунд**: игра стартует, когда в комнате два игрока и оба нажали «готов» (свойство игрока `IsReady`). При уничтожении машины или выходе напарника мастер-клиент сбрасывает готовность и перезагружает сцену у всех.
- **Кодировка**: собственные скрипты хранятся в UTF-8 с BOM, правило задано в `.editorconfig`.
- **Звук**: громкость двигателя и музыки — параметры `EngineVolume` и `MusikVolume` в AudioMixer, значения сохраняются в `StaticHolder` на время сессии.
- **Рендер**: профили `PC_RPAsset` и `Mobile_RPAsset` в `Assets/Settings`.
