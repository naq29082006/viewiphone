namespace Viewiphone.Protocol
{
    public enum PacketType : ushort
    {
        Handshake    = 0x0001,
        Auth         = 0x0002,
        DeviceInfo   = 0x0003,
        VideoConfig  = 0x0004,
        VideoFrame   = 0x0005,
        Heartbeat    = 0x0006,
        Error        = 0x0007,
        Disconnect   = 0x0008,
        ClientConfig = 0x0009
    }

    [Flags]
    public enum PacketFlags : ushort
    {
        None        = 0x0000,
        Keyframe    = 0x0001,
        AuthSuccess = 0x0002,
        AuthFailed  = 0x0004,
        Ping        = 0x0008,
        Pong        = 0x0010
    }
}
