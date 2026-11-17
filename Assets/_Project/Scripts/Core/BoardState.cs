
using System;
using System.Collections.Generic;

namespace SnakeBlockJam.Core
{
    public sealed class BoardState
    {
        private readonly Dictionary<int, SnakeDefinition> _activeSnakes;
        private readonly int[] _occupancy;

        public LevelDefinition Level { get; }

        public int ActiveSnakeCount => _activeSnakes.Count;

        // Level boyunca başarılı kaçış sayısı. Başarısız hamle sıfırlamaz (R-FX-05).
        public int EscapedCount { get; private set; }

        // null = sınırsız hamle (L1–L5, R-MV-05).
        public int? RemainingMoves { get; private set; }

        // Her seferinde mevcut durumdan hesaplanır; saklanmaz.
        // Önce kazanma, sonra kaybetme (R-WIN-04): son hamle tahtayı
        // temizlerse sayaç 0 olsa bile sonuç Won olur.
        public GameStatus Status
        {
            get
            {
                if (_activeSnakes.Count == 0)
                    return GameStatus.Won;

                if (RemainingMoves == 0)
                    return GameStatus.Lost;

                return GameStatus.Playing;
            }
        }

        public BoardState(LevelDefinition level)
        {
            if (level == null)
                throw new ArgumentNullException(nameof(level));

            if (level.Width <= 0 || level.Height <= 0)
                throw new ArgumentException("Invalid board dimensions.");

            if (level.MoveLimit < 0)
                throw new ArgumentException("Move limit cannot be negative.");

            Level = level;
            RemainingMoves = level.MoveLimit;

            _activeSnakes = new Dictionary<int, SnakeDefinition>();

            _occupancy = new int[checked(
                level.Width * level.Height * 2
            )];

            foreach (var snake in level.Snakes)
            {
                if (snake == null || snake.Id <= 0)
                    throw new ArgumentException("Invalid snake.");

                if (_activeSnakes.ContainsKey(snake.Id))
                    throw new ArgumentException("Duplicate snake ID.");

                foreach (var segment in snake.Body)
                {
                    int index = GetIndex(segment);

                    if (_occupancy[index] != 0)
                        throw new ArgumentException(
                            "Overlapping snake segments."
                        );

                    _occupancy[index] = snake.Id;
                }

                _activeSnakes.Add(snake.Id, snake);
            }
        }

        public bool IsInside(Cell cell)
        {
            return cell.X >= 0 && cell.X < Level.Width
                && cell.Y >= 0 && cell.Y < Level.Height;
        }

        public int GetOccupant(LayeredCell position)
        {
            return _occupancy[GetIndex(position)];
        }

        public bool IsActive(int snakeId)
        {
            return _activeSnakes.ContainsKey(snakeId);
        }

        public SnakeDefinition GetSnake(int snakeId)
        {
            return _activeSnakes[snakeId];
        }

        // Başarılı kaçış: yılan modelden anında kalkar (R-CON-04), sayaç artar.
        // Animasyon bu çağrıyı beklemez; animasyon view'ın işidir.
        public void EscapeSnake(int snakeId)
        {
            Remove(snakeId);
            EscapedCount++;
        }

        // Kabul edilen her yılan dokunuşu 1 hamle harcar; başarılı da olsa
        // başarısız da (R-MV-02). Sınırsız level'da sayaç null kalır.
        // Bilerek Status'a bakmaz, sadece "harcanacak hamle var mı?" diye bakar.
        // Böylece aynı hamle içinde EscapeSnake önce de çağrılsa sonra da
        // çağrılsa sonuç aynıdır (bkz. R_WIN_04 testleri).
        public void ConsumeMove()
        {
            if (RemainingMoves == null)
                return;

            if (RemainingMoves == 0)
                throw new InvalidOperationException("No moves left.");

            RemainingMoves--;
        }

        // Kürek: yılanı kaldırır ama ne hamle ne kaçış sayacına dokunur (R-SHV-05).
        public void RemoveSnake(int snakeId)
        {
            Remove(snakeId);
        }

        private void Remove(int snakeId)
        {
            if (!_activeSnakes.TryGetValue(snakeId, out var snake))
                throw new InvalidOperationException(
                    $"Snake {snakeId} is not active."
                );

            // Sadece bu yılanın kendi slotları temizlenir; aynı hücrenin
            // diğer katmanındaki yılan etkilenmez (R-BRG-04, R-BRG-10).
            foreach (var segment in snake.Body)
            {
                _occupancy[GetIndex(segment)] = 0;
            }

            _activeSnakes.Remove(snakeId);
        }

        private int GetIndex(LayeredCell position)
        {
            if (!IsInside(position.Cell))
                throw new ArgumentOutOfRangeException(
                    nameof(position)
                );

            if (position.Layer != BoardLayer.Ground &&
                position.Layer != BoardLayer.BridgeTop)
                throw new ArgumentOutOfRangeException(
                    nameof(position)
                );

            int layer = (int)position.Layer;

            return layer * Level.Width * Level.Height
                 + position.Cell.Y * Level.Width
                 + position.Cell.X;
        }
    }
}
