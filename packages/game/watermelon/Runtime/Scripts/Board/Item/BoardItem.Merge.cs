namespace Mu3Library.Game.WatermelonGame.Board.Item
{
    public partial class BoardItem
    {
        protected bool _isMerging = false;
        public bool IsMerging => _isMerging;

        private object _mergeReservationOwner;

        public virtual bool CanMerge(BoardItem hitItem)
        {
            if (this == null ||
                this == hitItem ||
                hitItem == null ||
                _index != hitItem.Index ||
                _info == null ||
                hitItem.Info == null ||
                !IsTouching(hitItem) ||
                _isMerging ||
                hitItem.IsMerging)
            {
                return false;
            }

            return true;
        }

        public bool IsTouching(BoardItem hitItem)
        {
            if (this == null || hitItem == null)
            {
                return false;
            }

            return _collider.IsTouching(hitItem.Collider);
        }

        public void SetMergeState(bool value)
        {
            _isMerging = value;
            _mergeReservationOwner = null;
        }

        internal bool TryReserveMerge(object owner)
        {
            if (owner == null || _isMerging)
            {
                return false;
            }

            _isMerging = true;
            _mergeReservationOwner = owner;
            return true;
        }

        internal bool IsMergeReservedBy(object owner)
        {
            return owner != null &&
                _isMerging &&
                ReferenceEquals(_mergeReservationOwner, owner);
        }

        internal void ReleaseMergeReservation(object owner)
        {
            if (!IsMergeReservedBy(owner))
            {
                return;
            }

            _isMerging = false;
            _mergeReservationOwner = null;
        }
    }
}
