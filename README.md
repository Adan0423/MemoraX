<div align="center">

  # ⚡ MemoraX

  **Utilidad moderna para Windows 11 para la visualización, gestión de Standby Memory y monitoreo térmico/hardware en tiempo real.**

  [![Windows 11](https://img.shields.io/badge/OS-Windows%2011%20x64-0078D4?style=for-the-badge&logo=windows11&logoColor=white)](https://microsoft.com)
  [![.NET 9.0](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
  [![WinUI 3](https://img.shields.io/badge/UI-WinUI%203-0078D4?style=for-the-badge&logo=windows&logoColor=white)](https://learn.microsoft.com/windows/apps/winui/winui3/)
  [![C#](https://img.shields.io/badge/Language-C%23%2013-239120?style=for-the-badge&logo=csharp&logoColor=white)](https://docs.microsoft.com/dotnet/csharp/)
  [![License](https://img.shields.io/badge/License-MIT-blue?style=for-the-badge)](LICENSE)

  <p align="center">
    <a href="#-acerca-del-proyecto">Acerca del Proyecto</a> •
    <a href="#-características-principales">Características</a> •
    <a href="#-stack-tecnológico">Stack Tecnológico</a> •
    <a href="#-arquitectura-del-proyecto">Arquitectura</a> •
    <a href="#-instalación-y-ejecución">Instalación</a> •
    <a href="#-detalles-técnicos--seguridad">Seguridad</a>
  </p>

</div>

---

## 📌 Acerca del Proyecto

**MemoraX** (Standby Memory Manager) es una aplicación de escritorio nativa para Windows 11 diseñada para ofrecer una experiencia fluida y elegante al monitorizar la memoria del sistema y el estado de tu hardware.

Construida con la arquitectura **WinUI 3** y **.NET 9**, MemoraX te permite consultar con precisión la memoria RAM (incluida la *Standby List*), efectuar limpiezas manuales seguras con llamadas NT del sistema operativo, y monitorear sensores térmicos y de carga de CPU/GPU en tiempo real a través de un widget flotante siempre visible (*Always-On-Top*) y un completo dashboard con estética Fluent y efectos Mica.

---

## 🚀 Características Principales

| Icono | Función | Descripción |
| :---: | :--- | :--- |
| 🪟 | **Widget Flotante Always-On-Top** | Mantiene un panel compacto de acceso rápido siempre visible para vigilar métricas clave sin interrumpir tus tareas. |
| 🧹 | **Limpieza de Standby Memory** | Ejecuta la purga explícita de la memoria caché Standby mediante llamadas nativas Win32/NT (`NtSetSystemInformation`). |
| 📊 | **Monitoreo de RAM Completo** | Visualización en tiempo real de RAM Total, Usada, Disponible y Standby List tanto en gigabytes (GB) como en porcentaje. |
| 🌡️ | **Monitoreo Térmico & Carga** | Lectura continua de temperatura de CPU, GPU (incluyendo Hotspot), uso de VRAM y velocidad de ventiladores (RPM). |
| 📑 | **Dashboard de Procesos** | Panel detallado con interfaz Mica/Dark UI que presenta la lista de procesos ordenada por consumo de *Working Set*. |
| ⚙️ | **PDH Multi-idioma** | Integración con `PdhAddEnglishCounter` para asegurar compatibilidad universal independientemente del idioma de Windows. |

---

## 🛠️ Stack Tecnológico

El proyecto aprovecha las tecnologías más modernas y eficientes para el desarrollo en el ecosistema Windows:

### **Lenguaje & Runtime**
- 🔷 **[C# 13](https://docs.microsoft.com/dotnet/csharp/)** — Lenguaje principal estructurado con tipado fuerte, alto rendimiento y sintaxis moderna.
- 💜 **[.NET 9 SDK](https://dotnet.microsoft.com/)** — Framework y runtime optimizado de última generación para aplicaciones de escritorio.

### **Interfaz de Usuario & Diseño**
- 🎨 **[WinUI 3 / Windows App SDK 2.4.0](https://learn.microsoft.com/windows/apps/winui/winui3/)** — Sistema UI nativo de Windows 11 con controles Fluent Design, animación suave y efectos Mica.
- ⚡ **[CommunityToolkit.Mvvm 8.4.2](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)** — Arquitectura MVVM desacoplada utilizando Source Generators (`[ObservableProperty]`, `[RelayCommand]`).

### **Acceso a Hardware & Sistema**
- 💻 **[LibreHardwareMonitorLib 0.9.6](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)** — Biblioteca para la lectura precisa de sensores térmicos, cargas y ventiladores de CPU/GPU.
- 🛠️ **Win32 / NT API (`Pdh.dll`, `ntdll.dll`)** — P/Invoke de bajo nivel para contadores de rendimiento y gestión de la lista de memoria del kernel Windows.

---

## 🏗️ Arquitectura del Proyecto

```text
StandbyMemoryManager/
├── 📂 Interop/
│   └── NativeMethods.cs         # Definición P/Invoke de APIs Win32 y NT (NtSetSystemInformation, PDH)
├── 📂 Models/
│   ├── HardwareSnapshot.cs      # Estructura de datos para sensores de temperatura y carga
│   ├── MemorySnapshot.cs        # Snapshot del estado de RAM y Standby Memory
│   └── ProcessMemoryItem.cs     # Información de consumo por proceso (Working Set, memoria privada)
├── 📂 Services/
│   ├── HardwareMonitorService.cs# Lectura continua de sensores mediante LibreHardwareMonitorLib
│   ├── MemoryService.cs         # Métricas de memoria y ejecución aislada de purga NT
│   └── ProcessMemoryService.cs  # Diagnóstico y ordenamiento de procesos en ejecución
├── 📂 ViewModels/
│   └── MonitorViewModel.cs      # ViewModel central que coordina el flujo de datos y comandos MVVM
├── 📂 Views/
│   ├── WidgetWindow.xaml        # Interfaz compacta Always-On-Top para monitorización rápida
│   ├── DashboardWindow.xaml     # Ventana principal de detalles, sensores y lista de procesos
│   └── DashboardSection.cs      # Helper de navegación entre secciones del dashboard
├── 📂 Assets/                   # Iconos de la aplicación y capturas de diseño
├── App.xaml / App.xaml.cs       # Punto de entrada de la aplicación WinUI 3
└── StandbyMemoryManager.csproj  # Configuración del proyecto, runtime .NET 9 y dependencias
```

---

## 💻 Requisitos del Sistema

- 🪟 **Sistema Operativo**: Windows 11 x64 (Build 22000 o superior).
- 🛠️ **Entorno de Desarrollo**: Visual Studio 2022 / 2026 con cargas de trabajo de *Desarrollo de escritorio C#* y *Windows App SDK*.
- ⚙️ **SDK**: .NET 9 SDK (x64).
- 🛡️ **Permisos de Administrador**: Necesarios si se desea ejecutar la función de limpieza de memoria Standby (`SeProfileSingleProcessPrivilege`).

---

## ⚙️ Instalación y Ejecución

### **1. Clonar el repositorio**
```bash
git clone https://github.com/Adan0423/MemoraX.git
cd MemoraX
```

### **2. Opción A: Desde Visual Studio**
1. Abre la solución `StandbyMemoryManager.sln`.
2. Espera a que NuGet restaure los paquetes de dependencias.
3. Selecciona la plataforma **`x64`** y compila en `Release` o `Debug`.
4. Ejecuta la aplicación (preferiblemente como administrador).

### **3. Opción B: Desde PowerShell / Terminal**
```powershell
# Restaurar dependencias NuGet
dotnet restore

# Compilar proyecto para Windows x64
dotnet build -c Release -p:Platform=x64

# Ejecutar aplicación
dotnet run -c Release --no-build -p:Platform=x64
```

### **4. Opción C: Generar Instalador .exe y Paquete Portable (Producción)**
```powershell
# Ejecutar el empaquetador automático (Compila .NET, genera .zip portable y .exe con Inno Setup)
.\build_installer.ps1
```
*Los archivos finales se ubicarán en la carpeta `dist/`:*
- 📦 **Instalador Ejecutable**: `dist/MemoraX_Setup_v1.0.0.exe` (Instalador completo con accesos directos y permisos de Administrador).
- 🗜️ **Paquete Portable**: `dist/MemoraX_v1.0.0_Portable_x64.zip` (Versión autocontenida sin requerir instalación).

---


## 🔒 Detalles Técnicos & Seguridad

> [!IMPORTANT]
> **Principios de Diseño y Uso Responsable**
> - **Acción Manual Explícita**: La limpieza de la *Standby List* requiere un clic explícito del usuario. No se realizan limpiezas automáticas ni agresivas en segundo plano.
> - **Aislamiento en Capa NT**: La liberación de memoria se realiza a través de `MemoryService.PurgeStandby()`, invocando `NtSetSystemInformation` tras obtener `SeProfileSingleProcessPrivilege`.
> - **Monitoreo Seguro**: La lectura de sensores es 100% pasiva y de solo lectura. No se alteran frecuencias, voltajes, BIOS ni curvas de ventilación.
> - **Caché de Sistema**: La memoria Standby es una caché reutilizable de Windows. Se recomienda vaciarla puntualmente para diagnósticos o pruebas de rendimiento.

---

<div align="center">
  <sub>Creado con ❤️ para la comunidad de Windows por <a href="https://github.com/Adan0423">Adan0423</a></sub>
</div>
