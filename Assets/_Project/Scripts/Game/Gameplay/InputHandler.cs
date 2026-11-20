using System.Collections.Generic;
using SnakeBlockJam.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SnakeBlockJam.Game
{
    // Mouse ve dokunmatik basışı → hücre → GameController.
    // Proje sadece yeni Input System'i kullanıyor (Player Settings: Active Input
    // Handling = Input System Package); eski UnityEngine.Input burada çalışmaz.
    // Pointer.current hem Mouse hem Touchscreen için ortak API'dir.
    public sealed class InputHandler : MonoBehaviour
    {
        [SerializeField] private GameController controller;

        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();

        private void Update()
        {
            Pointer pointer = Pointer.current;

            if (pointer == null || !pointer.press.wasPressedThisFrame)
                return;

            // UI dahil her basış "oyuncu burada" demektir (otomatik ipucu sayacı, R-HNT-04).
            controller.NotifyPlayerInput();
            HandlePress(pointer.position.ReadValue());
        }

        // Bir ekran noktasına basış. Hamle oynandıysa true döner.
        public bool HandlePress(Vector2 screenPosition)
        {
            // UI'a (ör. Restart butonu) yapılan dokunuş tahtaya geçmez.
            if (IsOverUI(screenPosition))
                return false;

            return TryPickSnake(screenPosition, out int snakeId) && controller.HandleSnakeTap(snakeId);
        }

        // Ekran noktasındaki yılan; hamle oynamaz. Önce görünen parçalar (göz ve
        // baş dahil); açılış çiziminde parçalar henüz ölçeklenmemişken hücre
        // düzlemi yedektir.
        public bool TryPickSnake(Vector2 screenPosition, out int snakeId)
        {
            BoardView board = controller.BoardView;

            if (board.TryPickSnake(controller.GameCamera, screenPosition, out snakeId))
                return true;

            snakeId = 0;
            return board.TryScreenToCell(controller.GameCamera, screenPosition, out Cell cell) &&
                   controller.TryGetSnakeAt(cell, out snakeId);
        }

        // Basış anında UI raycast'i: mouse ve dokunma için aynı çalışır.
        public bool IsOverUI(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;

            if (eventSystem == null)
                return false;

            var pointerData = new PointerEventData(eventSystem) { position = screenPosition };
            _uiHits.Clear();
            eventSystem.RaycastAll(pointerData, _uiHits);
            return _uiHits.Count > 0;
        }
    }
}
