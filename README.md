# ServiceBench

## Сборка и публикация через Visual Studio Code

1. Установите [Visual Studio Code](https://code.visualstudio.com/) и расширение **C# Dev Kit** либо классическое расширение **C#** от Microsoft.
2. Установите [SDK .NET 8.0 x64](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
3. Клонируйте репозиторий и откройте корневую папку проекта (`ServiceBench`) в VS Code.
4. VS Code автоматически предложит восстановить зависимости; при необходимости выполните команду **Restore** из списка задач (`Terminal → Run Task…`).
5. Для сборки приложения выберите задачу **Build** (`Terminal → Run Task… → Build`) или запустите из встроенного терминала:
   ```powershell
   dotnet build ServiceBench.sln -c Release
   ```
6. Для публикации единого исполняемого файла выполните задачу **Publish Single File** либо запустите команду:
   ```powershell
   dotnet publish ServiceBench.App -c Release -r win-x64 \
       -p:PublishSingleFile=true \
       -p:IncludeAllContentForSelfExtract=true \
       -p:PublishTrimmed=false
   ```
7. Готовый пакет появится в каталоге `ServiceBench.App/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish`.

### Если сборка завершается ошибкой «файл доступен только для чтения»

В некоторых случаях после распаковки архива Windows помечает вложенные файлы как read-only. Начиная с текущей версии проекта, при запуске `dotnet build`/`dotnet publish` права на файлы во внутренней папке `obj` автоматически снимаются, поэтому дополнительное ручное вмешательство не требуется. Если вы обновляете старую копию проекта, удалите каталоги `bin` и `obj` или снимите атрибут `Read-only` со всех файлов внутри них перед следующей сборкой.

## Структура задач VS Code

В каталоге `.vscode` добавлены преднастроенные задачи (`tasks.json`):
- **Restore** — выполняет `dotnet restore` для решения.
- **Build** — восстанавливает зависимости и собирает проект в конфигурации Release.
- **Publish Single File** — публикует приложение в формате единого EXE согласно требованиям ТЗ.

Вы можете запускать эти задачи через меню `Terminal → Run Task…` или назначить горячие клавиши в VS Code.
