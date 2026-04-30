# Инструкция компиляции StandKnifeLauncher

## Путь к проекту
```
c:/Users/ksn/Downloads/StandKnife 3.1 (F1, PC)/StandKnifeLauncher
```

## Команды компиляции

### 1. Очистка папки obj (обязательно перед каждой сборкой)
```powershell
Remove-Item "c:/Users/ksn/Downloads/StandKnife 3.1 (F1, PC)/StandKnifeLauncher/obj" -Recurse -Force
```

### 2. Компиляция проекта
```powershell
dotnet build "c:/Users/ksn/Downloads/StandKnife 3.1 (F1, PC)/StandKnifeLauncher/StandKnifeLauncher.csproj" -c Release
```

### 3. Копирование exe в dist_single
```powershell
Copy-Item "c:/Users/ksn/Downloads/StandKnife 3.1 (F1, PC)/StandKnifeLauncher/bin/Release/net48/StandKnifeLauncher.exe" -Destination "c:/Users/ksn/Downloads/StandKnife 3.1 (F1, PC)/StandKnifeLauncher/dist_single/StandKnifeLauncher.exe" -Force
```

## Параметры проекта
- **Target Framework**: .NET Framework 4.8 (net48)
- **Runtime**: win-x86
- **Configuration**: Release
- **Output**: bin/Release/net48/StandKnifeLauncher.exe
- **Final destination**: dist_single/StandKnifeLauncher.exe

## Зависимости
- MaterialDesignThemes (4.9.0)
- MaterialDesignColors (2.1.4)
- Newtonsoft.Json (13.0.3)
- System.Management (5.0.0)
- Costura.Fody (5.7.0) - для встраивания зависимостей в один exe

## Важные моменты
1. Всегда очищать папку obj перед сборкой
2. Использовать именно net48, не net6.0
3. Копировать exe из bin/Release/net48 в dist_single
4. Working directory для команд: путь к проекту
