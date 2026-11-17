
namespace SnakeBlockJam.Core
{
    public enum MoveOutcome
    {
        // Baş tahta sınırından çıktı; yılan kaçtı (R-MOV-02).
        EscapedOffBoard,

        // Baş tünel hücresine girdi; yılan tünelden kaçtı (R-BUR-02, R-BUR-07).
        EscapedIntoBurrow,

        // Baş engele çarptı; yılan aynı yoldan geri döner (R-COL-01, R-COL-05).
        Blocked
    }
}
