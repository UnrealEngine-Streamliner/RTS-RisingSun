public struct UnitDeselectedEvent : IEvent
{
    public ISelectable Unit { get; private set; }

    public UnitDeselectedEvent(ISelectable unit)
    {
        Unit = unit;
    }
}