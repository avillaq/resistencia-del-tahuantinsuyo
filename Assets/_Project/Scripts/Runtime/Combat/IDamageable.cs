using UnityEngine;

namespace ResistenciaTahuantinsuyo.Runtime.Combat
{
    /// <summary>
    /// Interfaz obligatoria para cualquier entidad que reciba daño según product.md (Sección 7.6 y 10.1).
    /// </summary>
    public interface IDamageable
    {
        int CurrentHealth { get; }
        int MaxHealth { get; }
        bool IsDead { get; }
        void TakeDamage(int amount, Vector2 hitDirection);
        void Heal(int amount);
    }
}
