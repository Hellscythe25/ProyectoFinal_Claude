using System;
using TTTXO.Core;
using UnityEngine;

namespace TTTXO.Game.UI.Screens
{
    /// <summary>
    /// Database of character animation frames for the RPS VS Screen.
    /// Stores individual frame Texture2Ds (extracted from spritesheets at build time)
    /// so they work at runtime without requiring readable atlas textures.
    /// </summary>
    [CreateAssetMenu(fileName = "RpsCharacterDatabase", menuName = "TTTXO/RPS Character Database")]
    public sealed class RpsCharacterDatabase : ScriptableObject
    {
        [Header("Guerrero (Piedra)")]
        public Texture2D[] Caballero_Idle;
        public Texture2D[] Caballero_Attack;
        public Texture2D[] Caballero_Hurt;

        [Header("Maga (Papel)")]
        public Texture2D[] Maga_Idle;
        public Texture2D[] Maga_Attack;
        public Texture2D[] Maga_Hurt;

        [Header("Asesino (Tijera) - optional")]
        public Texture2D[] Asesino_Idle;
        public Texture2D[] Asesino_Attack;
        public Texture2D[] Asesino_Hurt;

        public (Texture2D[] Idle, Texture2D[] Attack, Texture2D[] Hurt) GetFrames(RpsMove move)
        {
            return move switch
            {
                RpsMove.Rock => (Caballero_Idle, Caballero_Attack, Caballero_Hurt),
                RpsMove.Paper => (Maga_Idle, Maga_Attack, Maga_Hurt),
                RpsMove.Scissors => (Asesino_Idle, Asesino_Attack, Asesino_Hurt),
                _ => (Array.Empty<Texture2D>(), Array.Empty<Texture2D>(), Array.Empty<Texture2D>())
            };
        }

        public string GetDisplayName(RpsMove move)
        {
            return move switch
            {
                RpsMove.Rock => "Guerrero",
                RpsMove.Paper => "Maga",
                RpsMove.Scissors => "Asesino",
                _ => "?"
            };
        }
    }
}