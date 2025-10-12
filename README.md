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

### Если при сборке запрашиваются справочные сборки «.NETFramework,Version=v6.0»

Эта ошибка («error MSB3644: The reference assemblies for .NETFramework,Version=v6.0 were not found») появлялась из-за встроенной MSBuild-задачи, написанной на C# — для её компиляции требовались пакеты разработчика .NET Framework, которых нет в стандартной установке .NET SDK. Начиная с текущей версии проекта задача заменена простым вызовом `attrib`, поэтому дополнительная установка SDK больше не нужна. Чтобы исправить проблему:

1. Обновите проект до последней версии (например, `git pull`).
2. Повторите сборку: `dotnet build ServiceBench.sln -c Release`.

Если вы не можете обновиться, откройте файл `ServiceBench.App/ServiceBench.App.csproj` и удалите блок `<UsingTask ...>`/`<ClearReadOnlyAttributes ...>`, заменив его на цель:

```xml
  <Target Name="EnsureObjWritable" BeforeTargets="ResolveReferences" Condition="'$(OS)' == 'Windows_NT'">
    <Exec Command="attrib -R &quot;$(BaseIntermediateOutputPath)*&quot; /S /D" IgnoreExitCode="true" />
  </Target>
```

После сохранения пересоберите проект — ошибка больше не появится.

### Если MSBuild сообщает «Task factory "CodeTaskFactory" is not supported» (MSB4801)

Эта ошибка означает, что в вашем локальном `.csproj` всё ещё осталась старая встроенная задача `CodeTaskFactory`. Сам проект больше не использует её, однако при обновлении из архива или при наличии локальных изменений файл мог не замениться автоматически.

Исправление:

1. Убедитесь, что у вас последняя версия репозитория (`git pull`).
2. Выполните в корне проекта (подойдёт PowerShell 7 или классический Windows PowerShell):
   ```powershell
   pwsh ./tools/fix-inline-task.ps1
   ```
   либо
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\fix-inline-task.ps1
   ```
   Скрипт удалит блок `<UsingTask ...>` и заменит его безопасной целью с `attrib`.
3. Повторите сборку: `dotnet build ServiceBench.sln -c Release`.

Если PowerShell недоступен, внесите правки вручную по инструкции из предыдущего раздела.

### Если XAML-редактор сообщает «RowDefinitions is read-only» (MC3065)

Эта ошибка появляется при попытке задать строки/столбцы `Grid` строкой (`RowDefinitions="..."`).
В актуальной версии проекта разметка уже переведена на корректный синтаксис с вложенными
элементами `<Grid.RowDefinitions>`/`<Grid.ColumnDefinitions>`. Обновите проект до последней
версии (`git pull`) и пересоберите решение. Если требуется исправить существующую копию
вручную, откройте `Views/MainWindow.xaml` и `Views/SettingsWindow.xaml` и замените атрибуты
`RowDefinitions`/`ColumnDefinitions` на блоки вида:

```xml
<Grid>
  <Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>
    ...
  </Grid.RowDefinitions>
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="*"/>
    ...
  </Grid.ColumnDefinitions>
  <!-- остальное содержимое -->
</Grid>
```

После сохранения ошибка MC3065 исчезнет.

### Если компилятор XAML сообщает «Property "Spacing" does not exist» (MC3072)

Свойство `Spacing` отсутствует в `StackPanel` WPF и доступно только в WinUI/UWP.
Начиная с текущей версии проекта элементы интерфейса используют отступы через `Margin`,
поэтому обновление до последнего состояния (`git pull`) устраняет ошибку автоматически.
При необходимости исправить старую копию вручную, откройте файл `Views/SettingsWindow.xaml`
и удалите атрибут `Spacing`, задав отступы на дочерних кнопках, например:

```xml
<StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
  <Button Margin="0 0 10 0" ... />
  <Button ... />
</StackPanel>
```

После сохранения пересоберите решение (`dotnet build ServiceBench.sln -c Release`).

### Если сборка падает с ошибками CS0246 (`Task`/`DateTime` не найдены)

Такие ошибки означают, что в файлах отсутствуют базовые пространства имён .NET.
В актуальной версии проекта нужные `using` уже добавлены. Обновите репозиторий до
последнего состояния (`git pull`) и выполните сборку ещё раз. Если требуется внести
правки вручную:

1. Откройте `Services/AidaController.cs` и `Services/FurMarkController.cs`, добавьте в блок
   директив строку `using System.Threading.Tasks;`.
2. Откройте `Services/FileManager.cs`, убедитесь, что вверху файла есть строка
   `using System;`.
3. Сохраните изменения и пересоберите решение: `dotnet build ServiceBench.sln -c Release`.

## Структура задач VS Code

В каталоге `.vscode` добавлены преднастроенные задачи (`tasks.json`):
- **Restore** — выполняет `dotnet restore` для решения.
- **Build** — восстанавливает зависимости и собирает проект в конфигурации Release.
- **Publish Single File** — публикует приложение в формате единого EXE согласно требованиям ТЗ.

Вы можете запускать эти задачи через меню `Terminal → Run Task…` или назначить горячие клавиши в VS Code.
