using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Yılan renginde küp kıymıklar: kürekle kaybolan her segmentte ve tünele
    // giren her segmentte (deliğin kenarından dışa). Sistem renk başına
    // paylaşılır (BoardDecorBuilder.CreateChips); yoksa hiçbir şey yayılmaz.
    internal static class SnakeChipEmitter
    {
        public static void EmitShovelChips(ParticleSystem chips, Vector3 position, int index)
        {
            if (chips == null)
                return;
            for (int i = 0; i < 3; i++)
            {
                float angle = index * 2.4f + i * 2.09f;
                var chip = new ParticleSystem.EmitParams
                {
                    position = position + Vector3.up * 0.25f,
                    velocity = new Vector3(Mathf.Cos(angle) * 2f, 3.1f, Mathf.Sin(angle) * 2f)
                };
                chips.Emit(chip, 1);
            }
        }

        public static void EmitBurrowChips(ParticleSystem chips, Vector3 center, int segment)
        {
            if (chips == null)
                return;

            for (int i = 0; i < 6; i++)
            {
                float angle = segment * 2.4f + i * Mathf.PI / 3f;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float spread = Mathf.Repeat(segment * 0.37f + i * 0.61f, 1f);
                var chip = new ParticleSystem.EmitParams
                {
                    position = center + radial * (BoardView.BurrowSize * 0.5f) + Vector3.up * 0.18f,
                    velocity = radial * (2.2f + spread * 1.8f) + Vector3.up * (2f + spread),
                    startSize = 0.10f + spread * 0.13f,
                    startLifetime = 0.22f + spread * 0.13f,
                    rotation = angle * Mathf.Rad2Deg
                };
                chips.Emit(chip, 1);
            }
        }
    }
}
