using System;

namespace SnakeBlockJam.EditorTools
{
    // Level JSON'u içe aktarılamadı. Mesaj level, yılan ve alan adını içerir.
    public sealed class LevelImportException : Exception
    {
        public LevelImportException(string message, Exception inner = null)
            : base(message, inner)
        {
        }
    }
}
