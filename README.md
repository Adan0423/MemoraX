# ⚡ Veltrixa

> Monitor nativo de memoria y hardware para Windows, con un widget flotante compacto.

[![Windows](https://img.shields.io/badge/Windows-10%202004%2B-0078D4?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-9-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WinUI 3](https://img.shields.io/badge/UI-WinUI%203-00A4EF?logo=microsoft)](https://learn.microsoft.com/windows/apps/winui/winui3/)
[![Arquitectura](https://img.shields.io/badge/arquitectura-x64-555555)](https://github.com/Adan0423/MemoraX)

Veltrixa es una aplicación de escritorio **exclusiva para Windows x64**. Permite consultar el uso de RAM, sensores de hardware y procesos desde un panel profesional o un widget flotante minimalista. La limpieza de la caché Standby siempre es manual y explícita.

## ✨ Funciones

- 🧩 **Widget flotante minimalista** siempre visible, con botón de optimización y menú de funciones mediante clic derecho.
- 🧠 **Memoria RAM** total, usada, disponible y caché Standby, con lecturas no disponibles ocultas del widget.
- 🌡️ **Sensores** de CPU y GPU: temperatura, carga, memoria gráfica y ventiladores cuando el equipo los expone.
- 📋 **Procesos** ordenados por consumo de memoria y actualizados de forma incremental.
- 🧹 **Optimización manual** de la caché Standby con estado y resultado compartidos entre widget y panel.
- 🎨 **Temas** claro, oscuro y del sistema, con una interfaz adaptable y accesible.
- 🚀 **Bajo consumo**: consultas fuera del hilo de UI, frecuencias coordinadas y muestreo reducido cuando la app no está visible.

> Standby es una caché reutilizable que ya forma parte de la memoria disponible. Su tamaño no representa por sí solo un problema. Veltrixa no limpia automáticamente, no sustituye lecturas ausentes por cero y no modifica frecuencias, voltajes ni curvas de ventilación.

## 🧱 Stack tecnológico

| Capa | Tecnología |
| --- | --- |
| Plataforma | Windows 10 2004 (build 19041) o posterior · Windows 11 |
| Lenguaje | C# con .NET 9 |
| Interfaz | WinUI 3 · Windows App SDK 2.4 |
| Patrón | MVVM con CommunityToolkit.Mvvm 8.4.2 |
| Hardware | LibreHardwareMonitorLib 0.9.6 |
| APIs nativas | Windows interop para memoria Standby y privilegios |
| Distribución | Publicación autocontenida `win-x64` · Inno Setup 6 |

## 📊 Monitoreo eficiente

El widget y el panel comparten un único `MonitoringCoordinator`. Cada consulta se ejecuta fuera del hilo de interfaz y se activa según visibilidad:

| Lectura | Frecuencia con interfaz visible |
| --- | --- |
| RAM | 2 s |
| Sensores | 3 s |
| Procesos | 5 s, solo en la pestaña Procesos |
| Disco | 60 s, cuando se muestra en el panel |

Cuando ninguna ventana está visible, RAM y sensores pasan a una lectura cada 30 segundos. Restaurar una ventana solicita una actualización inmediata.

## 🛠️ Desarrollo

### Requisitos

- Windows x64, compilación 19041 o posterior.
- SDK de .NET 9 x64.
- Windows SDK y herramientas de escritorio de Windows.
- Visual Studio 2022 o CLI de .NET.

### Compilar

```powershell
dotnet restore .\Veltrixa.csproj
dotnet build .\Veltrixa.csproj -c Release -p:Platform=x64
```

Para ejecutar el binario compilado:

```powershell
Start-Process .\bin\x64\Release\net9.0-windows10.0.19041.0\win-x64\Veltrixa.exe -Verb RunAs
```

La aplicación solicita elevación para la limpieza nativa de memoria y el acceso a determinados sensores.

## 📦 Distribución y actualización

El script publica una versión autocontenida y crea el portable y el instalador:

```powershell
.\build_installer.ps1
```

Archivos generados en `dist/`:

- `Veltrixa_v1.1.0_Portable_x64.zip` — extraer todo antes de ejecutar.
- `Veltrixa_Setup_v1.1.0_x64.exe` — instalador para Windows.

El instalador conserva el mismo `AppId` entre versiones, cierra Veltrixa durante la actualización y la vuelve a iniciar al terminar. Por ello, al ejecutar un Setup nuevo sobre una instalación existente, Inno Setup actualiza los archivos sin crear una segunda instalación. La versión se controla desde `Veltrixa.csproj`.

Para generar solo el portable:

```powershell
.\build_installer.ps1 -SkipInstaller
```

## 🗂️ Organización

```text
Veltrixa.sln / Veltrixa.csproj   Solución y metadatos
App.xaml / App.xaml.cs          Recursos y ciclo de vida
Interop/                        APIs nativas de Windows
Models/                         Modelos de memoria, hardware y procesos
Services/                       Muestreo, coordinación y limpieza
ViewModels/                     Estado compartido y comandos
Views/                          Widget y panel WinUI
Assets/                         Iconos y recursos visuales
build_installer.ps1             Publicación y paquetes
installer.iss                   Configuración de Inno Setup
```

## 📄 Licencia

Consulta la licencia del repositorio antes de redistribuir Veltrixa.
