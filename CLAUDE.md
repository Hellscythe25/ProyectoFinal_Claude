# Instrucciones del proyecto

Este repositorio es **Guerrero, Maga y Asesino**, un juego de piedra, papel o tijera. No es TTTXO/TicTacToe y no debe volver a incluir Unity Gaming Services, matchmaking, cuentas, nube, leaderboards ni las pantallas antiguas.

## Idioma y estilo

- Documentación y textos de jugador: español.
- Código, nombres de clases, métodos, variables, comentarios, logs y commits: inglés.
- Los textos de UI pasan por `UiText`/Unity Localization; no hardcodear texto visible en los controladores.
- Mantén el estilo existente: controladores de pantalla pequeños, lógica de reglas en `TTTXO.Core`, estado de sesión en `RpsSeries`.

## Arquitectura

- `Assets/Scenes/Rps.unity` es la **única** escena y la única entrada en Build Settings.
- `AppRoot` contiene `UIDocument` + `GameManager` + `ScreenRouter` + `AppBootstrap` + `SceneUiBootstrap`.
- `ScreenRouter` es el host de navegación: seis pantallas, un solo `UIDocument`, `ScreenHistory` para el botón atrás.
- `RpsSeries` es el estado de la serie (mejor de cinco). La IA puede empatar: elige entre las tres jugadas, sin excluir la del jugador.
- Single Player resuelve la ronda al elegir el jugador. Local 2P espera a que el jugador 2 elija.
- El VS Screen no resuelve nada: lee `RpsSeries.LastRound`, revela al rival a los 5,5 s y anima Attack/Hurt durante 1 s.
- Un empate muestra `EMPATE` y vuelve a Character Select; nunca sale al menú.
- La serie termina al llegar a 3. Rematch reinicia; Menu abandona la serie.

## Arte y animaciones

- No reintroduzcas `Animator`, `SpriteRenderer` ni los objetos de preview en `Rps.unity`.
- Los frames viven en `Assets/Resources/RPSFrames` y se referencian desde `Assets/Resources/RpsCharacterDatabase.asset`.
- Para regenerarlos: menú **TTTXO → Setup → Create RPS Character Database** o `-executeMethod TTTXO.Game.Editor.RpsCharacterDatabaseSetup.CreateDatabase`.
- Cada personaje usa un canvas común por personaje, alineado abajo al centro; no cambies pivotes a mano.

## Escena y build

- Para reparar referencias de la escena: **TTTXO → Setup → Fix RPS Scene**. Elimina previews viejos y pone `_startScreen` en `RpsMenu`.
- No vuelvas a añadir `Main.unity` ni escenas de TTTXO.
- `ProjectSettings` debe mantener `Assets/Scenes/Rps.unity` como `templateDefaultScene` y no debe contener IDs de UGS.

## Tests

- EditMode: reglas, IA, `RpsSeries`, historial de pantallas.
- PlayMode: `Assets/Scripts/Tests/PlayMode/RpsSceneSmokeTests.cs` carga la escena real y comprueba revelación y empate.
- Comandos en `README.md`.
- No uses `-quit` junto con `-runTests`.

## Deuda conocida

- Los namespaces internos siguen siendo `TTTXO.*`; es histórico y no se debe usar como excusa para reintroducir contenido TTTXO.
- Store/Settings son locales y la tienda no tiene catálogo.
- Las claves nuevas de localización usan fallback en inglés hasta regenerar las tablas.
