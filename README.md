# Mcalculator

Calculadora científica, programable y con gráficas para Windows. Rápida, offline, gratis y sin cuentas.

![Mcalculator](App/Mcalculator.App/Assets/logo.png)

## Qué hace

- **Científica**: expresiones completas con prioridad de operadores, paréntesis, potencias, raíces, logaritmos, trigonometría (grados o radianes), factoriales, MCD/MCM y 25 funciones.
- **Constantes y variables**: `pi`, `e`, `tau`, `phi`, y variables propias con `x = 5` para reutilizarlas.
- **Historial y memoria**: cada cálculo se guarda y se puede reutilizar con doble clic; memoria con MC/MR/M+/M−/MS y `Ans` para el último resultado.
- **Unidades**: 9 categorías (longitud, masa, temperatura, área, volumen, tiempo, velocidad, energía y datos) con factores exactos.
- **Programador**: HEX, DEC, OCT y BIN sincronizados al escribir en cualquiera.
- **Gráficas**: dibuja `y = f(x)`, varias funciones a la vez separadas por `;`, con zoom con la rueda y arrastre para mover.
- **Teclado físico**: números, operadores, `Enter` para calcular, `Esc` para limpiar, `Ctrl+Z` para deshacer y `Ctrl+C` para copiar.

## Atajos

| Tecla | Acción |
| --- | --- |
| `Enter` | Calcular |
| `Esc` | Limpiar |
| `Retroceso` | Borrar un carácter |
| `Ctrl+Z` | Deshacer la última entrada |
| `Ctrl+C` | Copiar el resultado |

## Descarga

Última versión: **0.1.0**. Todas las versiones están en [Releases](https://github.com/montyxace-sys/Mcalculator/releases).

| Formato | Nota |
| --- | --- |
| Portable (EXE, x64) | No necesita instalar ni .NET |
| Instalador (MSI, x64) | Instalación por usuario, con accesos directos en Inicio y escritorio |

## Privacidad

Todo se calcula en tu equipo. No hay cuentas, ni analítica, ni conexión a ningún servidor. El historial y las preferencias se guardan en `%AppData%\Mcalculator` y se pueden borrar desde la propia aplicación.

## Compilar

```powershell
dotnet build Mcalculator.slnx -c Release --warnaserror
dotnet run --project tests/Mcalculator.CoreTests/Mcalculator.CoreTests.csproj -c Release
```

El instalador:

```powershell
dotnet publish App/Mcalculator.App/Mcalculator.App.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o entregas/portable

wix build installer/Mcalculator.wxs -arch x64 -d SourceDir=entregas/portable `
  -d IconFile=App/Mcalculator.App/Assets/mcalculator.ico -o entregas/msi/Mcalculator-0.1.0-win-x64.msi
```

## Créditos

- Motor de expresiones: [NCalc](https://github.com/ncalc/ncalc) (MIT).
- El diseño se inspira en SpeedCrunch y KCalc (GPL-2.0) y en NCalc Android (GPL-3.0): **no se ha copiado código de ellos**, solo la idea de evaluar la expresión completa y la potencia del teclado científico.

## Licencia

Ver [LICENSE](LICENSE).
