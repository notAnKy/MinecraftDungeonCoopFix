namespace DungeonCoopFix;

internal interface IControllerSlots
{
    int[] GetConnectedSlots();
}

internal sealed class XInputControllers : IControllerSlots
{
    public int[] GetConnectedSlots()
    {
        var connected = new List<int>(4);
        for (uint index = 0; index < 4; index++)
            if (NativeMethods.XInputGetState(index, out _) == 0) connected.Add((int)index);
        return connected.ToArray();
    }
}
