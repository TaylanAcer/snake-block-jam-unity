namespace SnakeBlockJam.Game
{
    // Bölüm başında gösterilen tanıtım kartı (R-FX-08). Kartın metni ve görseli
    // TutorialView'dadır; hangi bölümde hangisinin çıkacağı LevelData'dadır.
    public enum TutorialKind
    {
        None,
        Bridge,
        Burrow,
        Hint,
        Shovel,
        Arrow,
        Tap
    }
}
