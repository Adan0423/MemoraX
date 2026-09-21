# Veltrixa

Veltrixa es una utilidad de escritorio nativa y exclusiva para Windows x64. Permite consultar memoria RAM, sensores de hardware y procesos desde un panel y un widget flotante, y vaciar manualmente la caché Standby cuando se necesita realizar una prueba o un diagnóstico.

La interfaz está construida con WinUI 3 y .NET 9. El proyecto se configura para Windows 10, versión 2004 (compilación 19041), o posterior, incluido Windows 11. La disponibilidad de sensores depende del equipo y sus controladores.

## Funciones

- Widget flotante compacto siempre encima; sus acciones aparecen con clic derecho para no ocupar espacio permanente.
- Botón compacto de optimización dentro del widget para ejecutar la limpieza manual de Standby con un toque.
- Memoria total, en uso, disponible y caché Standby con unidades y estados de lectura explícitos.
- Temperaturas y carga de CPU y GPU, memoria gráfica y ventiladores cuando el hardware proporciona esos datos.
- Lista de procesos por consumo de memoria, con actualización incremental.
- Limpieza manual coordinada entre las ventanas, con progreso y resultado compartidos.
- Temas claro, oscuro y del sistema, navegación accesible y distribución adaptable al tamaño de la ventana.

Standby es una caché reutilizable que **ya forma parte de la memoria disponible**. Su tamaño no representa, por sí solo, un problema ni memoria perdida. Veltrixa no realiza limpiezas automáticas ni promete acelerar el equipo. Una lectura fallida o un sensor ausente se presenta como no disponible, sin sustituirlo por un cero o por otro sensor.

## Monitoreo y eficiencia

El widget y el panel comparten un único coordinador de monitoreo. Las consultas se ejecutan fuera del hilo de interfaz y se programan según el dato y la visibilidad:

| Dato | Frecuencia con la interfaz visible |
| --- | --- |
| Memoria RAM | 2 segundos |
| Sensores de hardware | 3 segundos |
| Procesos | 5 segundos, solo en su sección visible |
| Espacio en discos | 60 segundos, cuando se muestra en el panel |

Cuando ninguna ventana está visible, el monitoreo de memoria y hardware se reduce a una consulta cada 30 segundos. Abrir o restaurar una ventana solicita datos actualizados. La limpieza es una operación única compartida: ambas ventanas reflejan el mismo estado y solo una limpieza correcta actualiza la fecha de éxito.

## Compilar y ejecutar

Requisitos de desarrollo:

- Windows x64, compilación 19041 o posterior.
- SDK de .NET 9 x64.
- Herramientas de compilación para escritorio Windows y Windows SDK. También se puede abrir la solución con Visual Studio y las herramientas de WinUI instaladas.

Desde la carpeta del proyecto:

```powershell
dotnet restore .\Veltrixa.csproj
dotnet build .\Veltrixa.csproj -c Release -p:Platform=x64
```

En Visual Studio, abre `Veltrixa.sln`, selecciona `x64` y compila o ejecuta. Para iniciar el ejecutable compilado desde PowerShell:

```powershell
Start-Process .\bin\x64\Release\net9.0-windows10.0.19041.0\win-x64\Veltrixa.exe -Verb RunAs
```

El manifiesto solicita privilegios de administrador al iniciar la aplicación. Estos se utilizan para la limpieza nativa y el acceso a determinados sensores; Windows muestra su solicitud de elevación correspondiente.

## Crear paquetes

```powershell
.\build_installer.ps1
```

El script publica una aplicación autocontenida para Windows x64 en una carpeta nueva dentro de `artifacts/publish/` y genera:

- `dist/Veltrixa_v1.0.0_Portable_x64.zip`.
- `dist/Veltrixa_Setup_v1.0.0_x64.exe`, si encuentra el compilador `ISCC.exe` de Inno Setup 6.

La versión se lee de `Veltrixa.csproj`. Si ya existe un paquete con el mismo nombre, el nuevo incluye una marca de tiempo; no se borran los paquetes anteriores. El script no instala herramientas en el equipo. Para generar únicamente el portable:

```powershell
.\build_installer.ps1 -SkipInstaller
```

Extrae el ZIP completo antes de abrir `Veltrixa.exe`; el ejecutable necesita los archivos que lo acompañan. El instalador permite crear un acceso directo y conserva el identificador de instalación para reconocer versiones anteriores. No configura el inicio automático con Windows.

## Organización del código

```text
Veltrixa.sln / Veltrixa.csproj   Solución, metadatos y dependencias
App.xaml / App.xaml.cs          Recursos y ciclo de vida de la aplicación
Interop/                       APIs nativas de Windows
Models/                        Lecturas de memoria, hardware y procesos
Services/                      Muestreo, coordinación y limpieza manual
ViewModels/                    Estado compartido y comandos de interfaz
Views/                         Widget y panel WinUI
Assets/                        Iconos y recursos visuales
build_installer.ps1             Publicación y paquetes
installer.iss                  Definición del instalador Inno Setup
```

Dependencias fijadas: Windows App SDK `2.4.0`, CommunityToolkit.Mvvm `8.4.2` y LibreHardwareMonitorLib `0.9.6`. Las lecturas de hardware son pasivas: la aplicación no modifica frecuencias, voltajes ni curvas de ventilación. La purga se ejecuta mediante la API nativa de Windows y requiere una acción manual explícita.
