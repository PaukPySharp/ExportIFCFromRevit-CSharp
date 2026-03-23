# Структура конфигурации IFC-экспорта (пример)

Этот файл описывает пример структуры папки с настройками IFC-экспорта,
которая лежит в `_examples/IFC_Export_Config_example/`.

ExportIFC не использует эту примерную папку напрямую. Она нужна как
понятный ориентир: по ней настраивают реальный рабочий каталог
IFC-конфигов и сверяют его структуру.

Этот документ нужен как короткая карта файловой раскладки IFC-конфигов.
Он не дублирует весь manual, а помогает быстро понять,
где лежит общий слой, где проектный слой и как эта структура
связывается с `settings.ini` и `manage.xlsx`.

Путь к рабочему каталогу задаётся в `_settings/settings.ini`
через параметр `dir_export_config`.

Краткий сценарий первого запуска есть в корневом [README репозитория](../README.md),
а полный контракт между `settings.ini`, `manage.xlsx`, `Layer_Mapping.txt`,
`Export_Settings.json` и `Property_Mapping.txt` подробно разобран в
[основном manual](../_docs/ExportIFC_manual.md).

Дополнительные русскоязычные пояснения к этому примеру даны в
`_examples/IFC_Export_Config_structure_notes_ru.docx`.

---

## 1. Общая идея

Конфигурация IFC-экспорта разбита на три логических уровня:

| Уровень | Что обычно лежит | Роль в схеме |
| --- | --- | --- |
| `00_Common` | общие JSON-конфиги | общие шаблоны и профили, которые удобно переиспользовать |
| `01_Export_Layers` | `Layer_Mapping*.txt` | общие файлы сопоставления категорий Revit и классов IFC |
| `02_Project_*/` | `Export_Settings.json`, `Property_Mapping.txt` | проектные настройки конкретного IFC-сценария |

Важный практический контракт такой:

- `Layer_Mapping.txt` — это общий TXT-файл из `01_Export_Layers`,
  который add-in загружает как файл сопоставления категорий Revit
  и классов IFC;
- `Property_Mapping.txt` — это проектный файл, на который обычно
  ссылается `Export_Settings.json` внутри проектной папки;
- эти два файла не взаимозаменяемы и живут на разных уровнях структуры.

Именно такую схему и стоит воспроизводить в рабочем каталоге
IFC-конфигов на сервере или локальной машине.

---

## 2. Обзор структуры папок (пример)

```text
  IFC_Export_Config_example/
  ├─ 00_Common/
  │  └─ Export_Settings_NotAttributes.json
  ├─ 01_Export_Layers/
  │  └─ Layer_Mapping.txt
  └─ 02_Project_example/
    ├─ Export_Settings.json
    └─ Property_Mapping.txt
```

В рабочем варианте:

- `IFC_Export_Config_example/` — это только примерная заготовка;
- реальный каталог IFC-конфигов задаётся значением `dir_export_config`
  в `_settings/settings.ini`;
- `02_Project_example` — это пример проектной папки вроде
  `02_MyProject`, `03_ShoppingMall_2025` и т. п.

---

## 3. Как эта структура связана с `settings.ini` и `manage.xlsx`

ExportIFC ищет файлы не «по догадке», а по связке из настроек и Excel:

- `dir_export_config` задаёт корень каталога конфигов;
- `dir_layers` задаёт имя подпапки с общими TXT-файлами маппинга;
- колонка **C** в `manage.xlsx` указывает проектную папку с
  `Export_Settings.json` и связанными файлами;
- колонка **D** в `manage.xlsx` задаёт имя TXT-файла из
  `01_Export_Layers` без пути и без расширения.

На практике это означает следующее:

- путь к общему файлу сопоставления категорий Revit и классов IFC
  собирается как `<dir_export_config>/<dir_layers>/<значение D>.txt`;
- проектный `Export_Settings.json` берётся из папки,
  указанной в колонке **C**;
- если `Export_Settings.json` ссылается на `Property_Mapping.txt`,
  этот файл тоже должен лежать в той же проектной папке
  или по тому пути, который ожидает сам JSON.

Практический мини-пример выглядит так:

- `dir_export_config = D:\BIM\IFC_Configs`
- `dir_layers = 01_Export_Layers`
- `config_json = Export_Settings`
- колонка **C** = `D:\BIM\IFC_Configs\02_MyProject`
- колонка **D** = `Layer_Mapping_Default`

Тогда ExportIFC будет искать:

- `D:\BIM\IFC_Configs\01_Export_Layers\Layer_Mapping_Default.txt`;
- `D:\BIM\IFC_Configs\02_MyProject\Export_Settings.json`;
- путь к `Property_Mapping.txt` — уже по ссылке из `Export_Settings.json`.

Именно поэтому при любом переименовании важно контролировать всю цепочку:

`settings.ini` → `manage.xlsx` → реальные папки и файлы.

Если поправить только один уровень, ExportIFC не найдёт нужные конфиги.

---

## 4. Переименование папок и файлов

Имена из примера (`00_Common`, `01_Export_Layers`, `02_Project_example`,
`Export_Settings.json`, `Export_Settings_NotAttributes.json`,
`Layer_Mapping.txt`, `Property_Mapping.txt`) не являются жёстко зашитыми
как единственно допустимые.

В рабочей конфигурации можно менять:

- корневой каталог конфигов через `dir_export_config`;
- имя общей подпапки через `dir_common`;
- имя подпапки с файлами сопоставления категорий Revit и классов IFC
  через `dir_layers`;
- базовое имя основного JSON через `config_json`;
- имена и расположение проектных папок через данные в `manage.xlsx`.

Но логика уровней должна оставаться прежней:

- общий уровень;
- уровень общих TXT-файлов сопоставления категорий Revit и классов IFC;
- уровень проектных JSON-конфигов и связанных файлов.

---

## 5. Что важно при настройке рабочей папки

Удобнее всего проверять рабочую папку в таком порядке:

1. в `_settings/settings.ini` убедиться, что `dir_export_config`,
   `dir_common`, `dir_layers` и `config_json`
   соответствуют фактической структуре;
2. в `manage.xlsx` проверить, что колонка **C**
   указывает на реальную проектную папку,
   а колонки **D** и **F** содержат именно имена файлов без каталогов и без расширений;
3. в проектной папке убедиться, что основной JSON
   и связанный `Property_Mapping.txt` лежат предсказуемо и не потеряны;
4. если используется no-map-маршрут, проверить,
   что имя JSON из колонки **F** действительно существует
   в ожидаемой общей подпапке.

Для порядка и сопровождения `Property_Mapping.txt` обычно хранят рядом
с `Export_Settings.json`, даже если фактический путь к нему
берётся из самого JSON.

---

## 6. Как обычно используют этот пример

Обычно пример из `_examples/IFC_Export_Config_example/` используют так:

1. создают отдельный рабочий каталог IFC-конфигов;
2. копируют в него базовую структуру примера;
3. адаптируют общие JSON-конфиги и TXT-файлы сопоставления категорий
   Revit и классов IFC;
4. создают проектные папки с `Export_Settings.json`
   и при необходимости `Property_Mapping.txt`;
5. настраивают `dir_export_config` в `_settings/settings.ini`;
6. проверяют, что `manage.xlsx` ссылается на реальные папки и файлы.

---

## 7. Где смотреть подробности

Подробности по связке путей, Excel и IFC-конфигов см. в следующих документах:

- [README репозитория](../README.md) — короткая входная точка;
- [основной manual](../_docs/ExportIFC_manual.md) — полный разбор quick start,
  `settings.ini`, `manage.xlsx` и структуры IFC-конфигов;
- `_examples/IFC_Export_Config_structure_notes_ru.docx` — дополнительные
  русскоязычные пояснения к этому примеру.
