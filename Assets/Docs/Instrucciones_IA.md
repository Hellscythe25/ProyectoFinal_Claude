# Plan de Vuelo y Directrices para IA: "Duelo de Gremios"

## 1. Contexto del Proyecto
*   **Motor:** Unity (6000.3.20f1)
*   **Estado Actual:** Juego local de Piedra, Papel o Tijera (Guerrero, Maga, Asesino) utilizando UI Toolkit. Arquitectura dividida en `TTTXO.Core` (lógica) y `TTTXO.Game` (interfaz).
*   **Objetivo Final:** Convertir el proyecto en un juego asíncrono en la nube utilizando Unity Gaming Services (UGS) basándose en el GDD oficial.

## 2. Reglas Estrictas de Desarrollo (¡LEER ANTES DE ESCRIBIR CÓDIGO!)
1.  **No reescribir la UI desde cero:** Utiliza la infraestructura existente de UI Toolkit (`ScreenRouter`, `main.uss`, `Root.uxml`).
2.  **Reutilizar el Core:** La resolución de combates DEBE usar la clase existente `RpsRulesEngine` (Guerrero > Asesino > Maga > Guerrero).
3.  **Sin Servidores en Tiempo Real:** El juego es 100% asíncrono. Toda la persistencia se maneja subiendo y descargando un archivo JSON en **Unity Cloud Save**.
4.  **No alucinar dependencias:** Solo usa los paquetes de UGS que el proyecto ya soportaba en su rama anterior (Authentication, Cloud Save, Matchmaker, Analytics).

## 3. Modelo de Datos (`DuelStateDto.cs`)
Toda partida se basará en esta estructura serializable para Cloud Save:
*   `match_id` (string)
*   `ronda_actual` (int)
*   `estado_partida` (string: "TU_TURNO", "ESPERANDO_RIVAL", "FINALIZADO")
*   `turno_de` (string: player_id)
*   `timestamp_ultimo_movimiento` (string ISO 8601)
*   `jugador1` / `jugador2` (Objetos con: id, nombre, hp [100 por defecto], y un arreglo de 3 strings para la secuencia_actual)

## 4. Lógica de Combate y Timeout (Resolución Local)
*   **Daño:** 15 HP por choque ganado. 0 HP por empate.
*   **Resolución:** El cliente que envía el turno final de la ronda descarga el JSON, resuelve los 3 choques localmente usando el `RpsRulesEngine`, aplica el daño a los HP, limpia las secuencias, suma 1 a la ronda y vuelve a subir el JSON.
*   **Timeout:** 10 minutos. Si `HoraActual - timestamp_ultimo_movimiento > 10 min`, se habilita el botón "Reclamar Victoria".

## 5. Hoja de Ruta de Implementación
*   **Fase 1:** Crear `DuelStateDto.cs` y los métodos base de serialización.
*   **Fase 2:** Crear la UI estática de la pantalla de combate (3 ranuras por jugador y barras de 100 HP).
*   **Fase 3:** Crear `DuelResolver.cs` (máquina de estados local simulada sin internet).
*   **Fase 4:** Integrar `AuthenticationService` (Login anónimo) y `CloudSaveService`.
