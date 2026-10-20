# Arte

Esta carpeta contiene el arte que usa el juego.

- `SpriteSheet/`: las tres hojas de sprites originales (`Guerrero`, `Maga`, `Asesino`). Se editan fuera de Unity y se importan como `Sprite (2D and UI)` en modo `Multiple`.
- `Generated/brand-ui/`: fondo del menú, iconos de moneda e iconos de los modos Single Player / Local 2P.

Los frames que consume el juego **no** se editan a mano: se extraen de las hojas a `Assets/Resources/RPSFrames/<Personaje>/<Clip>/` con la herramienta de Editor.

Para regenerarlos:

- Menú: **TTTXO → Setup → Create RPS Character Database**.
- O en batchmode con `-executeMethod TTTXO.Game.Editor.RpsCharacterDatabaseSetup.CreateDatabase`.

Cada frame se pega en un canvas transparente del tamaño máximo del personaje, alineado abajo al centro, para que el personaje no salte entre clips. El resultado se referencia desde `Assets/Resources/RpsCharacterDatabase.asset`.

El arte placeholder de TTTXO (fichas, tableros, avatares, badges, reacciones, tienda) se eliminó junto con las pantallas que lo usaban.
