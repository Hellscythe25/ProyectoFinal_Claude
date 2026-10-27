# Instrucciones del proyecto

Este repositorio es **Guerrero, Maga y Asesino**, un juego de piedra, papel o tijera. No es TTTXO/TicTacToe y no debe volver a incluir las pantallas antiguas de TTTXO, leaderboards ni matchmaking en tiempo real.

Desde la rama `feature/duelo-asincrono` el proyecto es además **Duelo de Gremios**: un duelo asíncrono en la nube sobre el mismo RPS. Solo se admiten los paquetes UGS que el proyecto ya usaba antes: **Authentication** (login anónimo) y **Cloud Save** (persistencia). Nada de servidores en tiempo real, Relay, Lobby, Netcode ni Matchmaker.

## Idioma y estilo

- Documentación y textos de jugador: español.
- Código, nombres de clases, métodos, variables, comentarios, logs y commits: inglés.
- Los textos de UI pasan por `UiText`/Unity Localization; no hardcodear texto visible en los controladores.
- Los campos del documento JSON (`match_id`, `nombre`, `hp`, `secuencia_actual`…) son el contrato y van en snake_case/español a propósito. El código usa los alias en inglés de `DuelStateDto`/`DuelPlayerDto`.
- Mantén el estilo existente: controladores de pantalla pequeños, lógica de reglas en `TTTXO.Core`, estado de sesión en `RpsSeries` (local) y `DuelHub` (asíncrono).

## Arquitectura

- `Assets/Scenes/Rps.unity` es la **única** escena y la única entrada en Build Settings.
- `AppRoot` contiene `UIDocument` + `GameManager` + `ScreenRouter` + `AppBootstrap` + `SceneUiBootstrap`.
- `ScreenRouter` es el host de navegación: ocho pantallas, un solo `UIDocument`, `ScreenHistory` para el botón atrás.
- `RpsSeries` es el estado de la serie (mejor de cinco). La IA puede empatar: elige entre las tres jugadas, sin excluir la del jugador.
- Single Player resuelve la ronda al elegir el jugador. Local 2P espera a que el jugador 2 elija.
- El VS Screen no resuelve nada: lee `RpsSeries.LastRound`, revela al rival a los 5,5 s y anima Attack/Hurt durante 1 s.
- Un empate muestra `EMPATE` y vuelve a Character Select; nunca sale al menú.
- La serie termina al llegar a 3. Rematch reinicia; Menu abandona la serie.

## Duelo asíncrono

- Pantallas: `DuelHub` (`screen-duel-hub`, lista de duelos activos) y `DuelCombat` (`screen-duel`, 3 ranuras + barras de 100 HP). Se entra desde el botón **Duels** del menú.
- Reglas en `TTTXO.Core/Duel`: 100 HP, **15 de daño por choque ganado** y 0 por empate, 3 choques por ronda, timeout de 10 min (2 min con el toggle de demo de Settings) y tope de 30 rondas.
- La resolución **debe** usar `RpsRulesEngine` (Guerrero > Asesino > Maga > Guerrero). No dupliques la tabla de victorias.
- `DuelResolver` es una función pura sobre `DuelStateDto`: aplica el daño, limpia las secuencias, suma ronda y sella `timestamp_ultimo_movimiento`. No toca Unity, para que Cloud Code pueda ejecutar el mismo código como autoridad.
- `DuelTimeout` concentra la regla de abandono. Solo puede reclamar quien ya jugó y cuyo rival sigue sin jugar.
- `IDuelStore` abstrae la persistencia. `LocalDuelStore` guarda un JSON por partida en `persistentDataPath/duels`; `DuelCloudStore` espeja en local y empuja a Cloud Save en segundo plano.
- `DuelCloudService` usa **Cloud Save Player Data** (`duelIndex` + `duelMatch_<id>`), así que cada cuenta ve sus propios documentos. El PvP real necesitará game data/Cloud Code; no lo simules con Player Data.
- Sin proyecto UGS vinculado o sin red, todo degrada a local con un warning: el juego nunca debe bloquearse esperando la nube.
- El rival de práctica contesta en la **siguiente** apertura del duelo (así `ESPERANDO_RIVAL` es un estado real). El botón de rival silencioso solo existe en Editor/Development Build para demostrar el timeout.

## Arte y animaciones

- No reintroduzcas `Animator`, `SpriteRenderer` ni los objetos de preview en `Rps.unity`.
- Los frames viven en `Assets/Resources/RPSFrames` y se referencian desde `Assets/Resources/RpsCharacterDatabase.asset`.
- Para regenerarlos: menú **TTTXO → Setup → Create RPS Character Database** o `-executeMethod TTTXO.Game.Editor.RpsCharacterDatabaseSetup.CreateDatabase`.
- Cada personaje usa un canvas común por personaje, alineado abajo al centro; no cambies pivotes a mano.

## Escena y build

- Para reparar referencias de la escena: **TTTXO → Setup → Fix RPS Scene**. Elimina previews viejos y pone `_startScreen` en `RpsMenu`.
- No vuelvas a añadir `Main.unity` ni escenas de TTTXO.
- `ProjectSettings` debe mantener `Assets/Scenes/Rps.unity` como `templateDefaultScene`.
- El proyecto debe estar vinculado a un **Unity Project ID** (Project Settings → Services) para que Authentication/Cloud Save funcionen. Ese vínculo se hace desde el Editor; no inventes IDs a mano ni los metas en el código.

## Tests

- EditMode: reglas, IA, `RpsSeries`, historial de pantallas, `DuelResolver`/`DuelTimeout`, serialización del contrato JSON y el bucle de `DuelHub`.
- PlayMode: `Assets/Scripts/Tests/PlayMode/RpsSceneSmokeTests.cs` carga la escena real y comprueba revelación y empate.
- Comandos en `README.md`.
- No uses `-quit` junto con `-runTests`.

## Deuda conocida

- Los namespaces internos siguen siendo `TTTXO.*`; es histórico y no se debe usar como excusa para reintroducir contenido TTTXO.
- Store/Settings son locales y la tienda no tiene catálogo.
- Las claves nuevas de localización usan fallback en inglés hasta regenerar las tablas.
- La animación completa choque a choque del duelo está pendiente: hoy se revelan las filas en secuencia con una sola pasada Attack/Hurt.
- El PvP real (dos cuentas sobre el mismo documento) requiere Cloud Code o Cloud Save game data; hoy el duelo es practica/asíncrono contra bot.
