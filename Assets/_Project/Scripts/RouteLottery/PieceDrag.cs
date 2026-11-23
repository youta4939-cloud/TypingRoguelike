using UnityEngine;
using UnityEngine.EventSystems;

namespace TypingRoguelike.RouteLottery
{
    public sealed class PieceDrag : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RouteLotteryPresenter Owner;
        public string PieceId;
        public int Row = -1;
        public int Col = -1;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && !eventData.dragging)
                Owner.ClickPiece(PieceId, Row, Col);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            eventData.eligibleForClick = false;
            if (Row >= 0)
                Owner.BeginCellDrag(Row, Col, eventData);
            else
                Owner.BeginPieceDrag(PieceId, eventData);
        }

        public void OnDrag(PointerEventData eventData) => Owner.DragMove(eventData);

        public void OnEndDrag(PointerEventData eventData) => Owner.EndPieceDrag(eventData);
    }
}
