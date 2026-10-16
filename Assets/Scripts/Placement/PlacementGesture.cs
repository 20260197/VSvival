namespace CookieBreaker.Placement
{
    // One captured pointer owns a gesture. Only its valid release may commit.
    public sealed class PlacementGesture
    {
        private bool active;
        private int pointer;
        public bool Begin(int pointerId)
        { if (active) return false; pointer = pointerId; active = true; return true; }
        public bool Owns(int pointerId) => active && pointer == pointerId;
        public bool Release(int pointerId, bool overField)
        { if (!Owns(pointerId)) return false; active = false; return overField; }
        public void Cancel() { active = false; }
    }
}
