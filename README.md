# Guerrero, Maga y Asesino

Juego de piedra, papel o tijera con tres combatientes animados:

| Combatiente | Jugada |
| --- | --- |
| Guerrero | Piedra |
| Maga | Papel |
| Asesino | Tijera |

Es un juego local para un jugador contra la IA o para dos jugadores en el mismo dispositivo. No usa servicios online.

## Cómo jugar

1. Abre el proyecto con **Unity 6000.3.20f1**.
2. Abre `Assets/Scenes/Rps.unity` (es la única escena del build).
3. Pulsa **Play**.

Flujo de una partida:

1. **Menú RPS** → `Play` → eliges **Single Player** o **Local 2P**.
2. **Character Select**: eliges combatiente. En Local 2P, el jugador 1 elige y luego el jugador 2.
3. **VS Screen**: el combatiente del jugador 1 aparece a la izquierda. La elección del rival (IA o jugador 2) se revela a los 5,5 segundos.
4. El ganador de la ronda reproduce `Attack`; el perdedor, `Hurt`, durante un segundo. Después se vuelve a Character Select.
5. Un empate muestra **EMPATE** durante tres segundos y la serie continúa.
6. La serie es al mejor de cinco (primero en llegar a 3). Al terminar aparecen **Rematch** y **Menu**.

## Pantallas

- `RpsMenu`: menú principal, Store y Settings.
- `RpsModeSelect`: Single Player / Local 2P.
- `RpsCharacterSelect`: los tres combatientes en Idle; también muestra ronda, turno y marcador.
- `RpsVs`: revelación, animaciones de ataque/daño, empate y fin de serie.
- `Store`: escaparate local sin catálogo real todavía.
- `Settings`: idioma, volúmenes y borrado de datos locales.

## Estructura

```
Assets/
  Scenes/Rps.unity                 única escena
  Scripts/Core/                    reglas RPS, IA y control de serie
  Scripts/Game/Bootstrap/          GameManager local (PlayerPrefs)
  Scripts/Game/UI/                 router, historial y textos
  Scripts/Game/UI/Screens/         controladores de pantalla
  Scripts/Game/Editor/             setup de escena, frames y localización
  Scripts/Tests/                   EditMode y PlayMode
  UI/Screens/Root.uxml             las seis pantallas
  UI/Styles/main.uss               estilos
  Resources/RpsCharacterDatabase.asset
  Resources/RPSFrames/             frames extraídos por personaje
  Art/SpriteSheet/                 hojas de sprites originales
```

## Animaciones

Los personajes no usan `Animator` ni `SpriteRenderer`. El editor extrae cada frame de las hojas de sprites a `Assets/Resources/RPSFrames/<Personaje>/<Clip>/`, lo pega en un canvas transparente del tamaño máximo del personaje (alineado abajo al centro) y lo guarda como `Texture2D`. `VSAnimationController` intercambia `background-image` en UI Toolkit.

Para regenerar los frames:

- Menú de Unity: **TTTXO → Setup → Create RPS Character Database**.
- O en batchmode:

```
Unity.exe -batchmode -nographics -quit ^
  -projectPath "<ruta del proyecto>" ^
  -executeMethod TTTXO.Game.Editor.RpsCharacterDatabaseSetup.CreateDatabase ^
  -logFile "Logs/RpsSetup.log"
```

## Tests

EditMode (reglas, IA, serie e historial de navegación):

```
Unity.exe -batchmode -nographics -projectPath "<ruta>" ^
  -runTests -testPlatform EditMode ^
  -testResults "Logs/EditModeTests.xml" ^
  -logFile "Logs/EditModeTests.log"
```

PlayMode (carga `Rps.unity`, revelación de la IA y empate en Local 2P):

```
Unity.exe -batchmode -projectPath "<ruta>" ^
  -runTests -testPlatform PlayMode ^
  -testResults "Logs/PlayModeTests.xml" ^
  -logFile "Logs/PlayModeTests.log"
```

No uses `-quit` con `-runTests`: Unity debe quedarse hasta que el runner termine.

## Notas

- El espacio de nombres interno sigue siendo `TTTXO.*` por historia del proyecto; no afecta al nombre del producto ni al contenido.
- Store y Settings son locales. No hay cuentas, nube, matchmaking ni compras.
- Las claves de localización nuevas (`RpsCharacterSelect/*`, `RpsGame/RoundFormat`, `Store/TabSkins`, `Store/TabArenas`) usan el texto de respaldo en inglés hasta que se regeneren las tablas de localización.
