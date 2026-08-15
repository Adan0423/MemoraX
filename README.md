# MemoraX

Utilidad moderna para Windows 11 creada con **C# + .NET 9 + WinUI 3 + MVVM** para visualizar la memoria Standby, liberarla manualmente y vigilar temperatura/carga de CPU y GPU.

## Funciones incluidas

- Widget flotante `always-on-top`.
- Standby Memory en GB y porcentaje.
- Clic en el bloque principal para ejecutar **Limpiar Standby**.
- RAM total, usada y disponible.
- Temperatura CPU.
- Temperatura GPU y hotspot cuando el sensor existe.
- Carga CPU/GPU.
- VRAM usada/total cuando el driver expone el sensor.
- RPM de ventiladores cuando el hardware expone el sensor.
- Panel de detalles con Mica/dark UI.
- Vista de procesos ordenada por Working Set.
- PDH con `PdhAddEnglishCounter`, evitando depender del idioma de Windows para los nombres de contadores.
- Limpieza aislada en `MemoryService` mediante Win32/NT.

## Nota importante sobre "Apps en caché"

La Standby List es una caché del administrador de memoria de Windows y **no se puede atribuir de forma fiable como X GB de Standby a cada aplicación**. Por eso la pestaña Procesos muestra Working Set y memoria privada como datos de diagnóstico, sin afirmar que sean Standby por proceso.

## Sensores

El proyecto utiliza `LibreHardwareMonitorLib` para leer sensores de hardware. Según placa, CPU, GPU, firmware y driver, algunos sensores pueden no estar disponibles. En ese caso la interfaz muestra `—`.

La lectura se realiza cada 3 segundos para mantener bajo el overhead. En ciertos equipos, consultar continuamente sensores GPU puede impedir que una GPU discreta permanezca en su estado de energía más bajo; si observas ese comportamiento, aumenta el intervalo o desactiva la lectura térmica.

## Requisitos

- Windows 11 x64.
- Visual Studio 2022/2026 con desarrollo de escritorio C# y Windows App SDK.
- .NET 9 SDK.
- Para liberar Standby Memory, puede ser necesario ejecutar como administrador.

## Dependencias

- `Microsoft.WindowsAppSDK` 2.4.0
- `CommunityToolkit.Mvvm` 8.4.2
- `LibreHardwareMonitorLib` 0.9.6

## Ejecutar

1. Abre `StandbyMemoryManager.sln`.
2. Espera a que NuGet restaure las dependencias.
3. Selecciona `x64`.
4. Compila en `Debug` o `Release`.
5. Ejecuta la aplicación. Si el botón de limpieza indica falta de privilegios, vuelve a abrir Visual Studio o el ejecutable como administrador.

También puedes probar desde terminal en Windows:

```powershell
dotnet restore
dotnet build -c Release -p:Platform=x64
```

## Arquitectura

```text
StandbyMemoryManager/
├── Interop/
│   └── NativeMethods.cs
├── Models/
│   ├── HardwareSnapshot.cs
│   ├── MemorySnapshot.cs
│   └── ProcessMemoryItem.cs
├── Services/
│   ├── HardwareMonitorService.cs
│   ├── MemoryService.cs
│   └── ProcessMemoryService.cs
├── ViewModels/
│   └── MonitorViewModel.cs
├── Views/
│   ├── WidgetWindow.xaml
│   ├── WidgetWindow.xaml.cs
│   ├── DashboardWindow.xaml
│   └── DashboardWindow.xaml.cs
├── Assets/
│   └── design-reference.png
├── App.xaml
├── App.xaml.cs
├── app.manifest
└── StandbyMemoryManager.csproj
```

## Seguridad y comportamiento

- La limpieza es siempre una acción explícita del usuario.
- El proyecto no modifica voltajes, clocks, ventiladores, BIOS ni perfiles térmicos.
- No existe limpieza automática agresiva por defecto.
- Standby Memory es caché reutilizable; vaciarla repetidamente puede empeorar el rendimiento. El botón existe para uso manual y medición antes/después.

## Limpieza de Standby

La implementación de limpieza está deliberadamente encapsulada en `MemoryService.PurgeStandby()`. Utiliza `NtSetSystemInformation(SystemMemoryListInformation, MemoryPurgeStandbyList)` después de solicitar `SeProfileSingleProcessPrivilege`.

Este mecanismo pertenece a la capa NT y no es una API WinUI de alto nivel. Mantenerlo aislado facilita sustituirlo si Microsoft cambia el comportamiento en una futura versión de Windows.
