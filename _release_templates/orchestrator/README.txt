README.txt для orchestrator-пакета

ExportIFCFromRevit-CSharp — Orchestrator package

Что это за пакет:
Этот пакет содержит внешний orchestrator для ручного и планового
batch-запуска IFC-выгрузки.

Он читает `_settings/settings.ini`, `admin_data/manage.xlsx`
и `admin_data/history/history.xlsx`,
формирует batch-пакеты, запускает нужные версии Revit
и сохраняет диагностическую информацию.

Что входит в пакет:
- `ExportIfc.Orchestrator.exe` — основной исполняемый файл;
- `_settings/settings.ini` — настройки запуска;
- `admin_data/` — рабочие Excel-файлы, история и логи;
- `_examples/` — стартовые примеры IFC-конфигов для first-run;
- `_docs/ExportIFC_manual_ru.docx` — практический manual для first-run,
  ручного запуска и быстрой диагностики;
- `run-local.cmd` — удобный first-run с живой консолью.

Что важно понять сразу:
- этот пакет сам по себе не устанавливает Revit add-in;
- для реального IFC-экспорта на машине должен быть отдельно установлен
  add-in для нужных версий Revit;
- для dry-run add-in не требуется, но для real-run без него
  экспорт не состоится.

Быстрый маршрут first-run:
1. Распакуйте архив в отдельную рабочую папку.
2. Не разрывайте структуру пакета: `ExportIfc.Orchestrator.exe`,
   `_settings`, `admin_data`, `_examples`, `_docs` и `run-local.cmd`
   должны оставаться в одном рабочем контуре.
3. Установите add-in package для нужных версий Revit.
4. Один раз вручную откройте нужные версии Revit и в окне подтверждения
   загрузки add-in нажмите `Загружать всегда`.
5. При необходимости скопируйте стартовый пример из `_examples`
   в рабочий каталог IFC-конфигов и настройте `_settings/settings.ini`.
6. Если нужен расширенный, но всё ещё практический маршрут,
   откройте `_docs/ExportIFC_manual_ru.docx`.
7. Проверьте `manage.xlsx` и выполните первый запуск через `run-local.cmd`
   в режиме dry-run.
8. После успешного dry-run переходите к real-run
   или к запуску по расписанию.

Как запускать пакет дальше:
- `run-local.cmd` удобно использовать для первого знакомства,
  dry-run и ручной диагностики: он запускает orchestrator
  прямо из корня этого пакета и оставляет консоль открытой;
- для штатной эксплуатации и Планировщика задач Windows обычно запускают
  `ExportIfc.Orchestrator.exe` напрямую;
- рабочим корнем должен оставаться каталог, где рядом лежат
  `ExportIfc.Orchestrator.exe`, `_settings`, `admin_data`
  и остальная раскладка пакета.

Где читать подробности:
- `_docs/ExportIFC_manual_ru.docx` — сокращённый практический manual
  по first-run, ручному запуску и диагностике;
- `_settings/README.md` — как читать `settings.ini` в release-сценарии;
- `_examples/IFC_Export_Config_structure.md` — как устроить
  рабочий каталог IFC-конфигов;
- GitHub-репозиторий проекта:
  `https://github.com/PaukPySharp/ExportIFCFromRevit-CSharp`;
- основной manual:
  `https://github.com/PaukPySharp/ExportIFCFromRevit-CSharp/blob/main/_docs/ExportIFC_manual.md`.
