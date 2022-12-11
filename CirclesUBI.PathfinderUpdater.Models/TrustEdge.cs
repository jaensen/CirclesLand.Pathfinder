namespace CirclesUBI.Pathfinder.Models;

public class TrustEdge
{
    public uint UserAddress { get; }
    public uint CanSendToAddress { get; }
    public byte Limit { get; }

    public TrustEdge(uint userAddress, uint canSendToAddress, byte limit)
    {
        UserAddress = userAddress;
        CanSendToAddress = canSendToAddress;
        Limit = limit;
    }
    
    public void Serialize(Stream stream)
    {
        stream.Write(BitConverter.GetBytes(UserAddress));
        stream.Write(BitConverter.GetBytes(CanSendToAddress));
        stream.WriteByte(Limit);
    }
}